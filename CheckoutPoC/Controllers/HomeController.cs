using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Checkout;
using System.Configuration;
using Checkout.ApiServices.Charges.RequestModels;
using Checkout.ApiServices.SharedModels;
using System.Web.ModelBinding;
using PayPal.Api;
using System.Net;
using System.IO;
using System.Runtime.Caching;
using System.Text;
using Checkout.ApiServices.Tokens.RequestModels;
using Checkout.ApiServices.Tokens;
using Checkout.ApiServices.Tokens.ResponseModels;
using CheckoutPoC;

namespace CheckoutComAndPayPalPoC.Controllers
{
	public class HomeController : Controller
	{
		public ActionResult Index()
		{
			var hooks = MemoryCache.Default["Hooks"] as Dictionary<string, object> ?? new Dictionary<string, object>();
			ViewBag.Hooks = hooks;

			return View();
		}

		public ActionResult About()
		{
			ViewBag.Message = "Your application description page.";

			return View();
		}

		public ActionResult Contact()
		{
			ViewBag.Message = "Your contact page.";

			return View();
		}

		[HttpGet]
		[ActionName("CreditCard")]
		public ActionResult GetCreditCard(string method)
		{
			ViewBag.Method = method;

			return View();
		}

		private Product CreateCheckoutProduct()
		{
			return new Product()
			{
				Name = "Kettle",
				Description = "It's a kettle",
				Price = 40m,
				Quantity = 1,
				ShippingCost = 4.50m,
				Sku = "1aab2aa",
				TrackingUrl = null
			};
		}

		private Checkout.ApiServices.SharedModels.Address CreateCheckoutAddress()
		{
			return new Checkout.ApiServices.SharedModels.Address()
			{
				AddressLine1 = "623 Slade Street",
				AddressLine2 = "Flat 9",
				Postcode = "E149SR",
				Country = "UK",
				City = "London",
				State = "Greater London",
				Phone = new Checkout.ApiServices.SharedModels.Phone()
				{
					CountryCode = "44",
					Number = "12345678"
				}
			};
		}

		private string GetCardType(string cardToken)
		{
			var client = CreateAPIClient();

			// https://docs.checkout.com/reference/merchant-api-reference/lookups/bin-lookup-via-card-token
			var response = client.GetBinLookupViaCardToken(cardToken);
			if (response.HasError)
			{
				throw new Exception(response.Error.Message);
			}
			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", response.HttpStatusCode));
			}
			var binInfo = response.Model;
			return binInfo.Type;
		}

		[HttpPost]
		[ActionName("CreditCard")]
		public ActionResult PostCreditCard([Bind(Prefix = "cko-card-token")]string cardToken)
		{
			// Create payload
			// https://docs.checkout.com/reference/merchant-api-reference/charges/charge-with-card-token

			var shippingAddress = CreateCheckoutAddress();
			var billingAddress = CreateCheckoutAddress();
			var product = CreateCheckoutProduct();
			var orderId = Guid.NewGuid();

			var amount = ((product.Price * product.Quantity) + product.ShippingCost);

			// Find out the card type
			var cardType = GetCardType(cardToken);
			if (cardType == "Credit")
				amount *= 1.025m; // 2% credit card surcharge

			var payload = new CardTokenCharge()
			{
				//AutoCapture = "Y", // transfer funds automatically
				//AutoCapTime = 0, // transfer funds immediately after authorisation
				AutoCapture = "N", // manually capture
				ChargeMode = 1, // non-3D
				Email = "pedro.chimighanga@freshegg.com",
				Description = "Order",
				Value = (amount * 100).ToString("0"),
				Currency = "GBP",
				TrackId = orderId.ToString(),
				TransactionIndicator = "1", // regular
				CustomerIp = Request.ServerVariables["REMOTE_ADDR"] ?? Request.ServerVariables["HTTP_X_FORWARDED_FOR"],
				CardToken = cardToken,
				ShippingDetails = shippingAddress,
				// billing address??
				Products = new List<Product>() { product }
			};

			var client = CreateAPIClient();
			// Authorise
			var response = client.ChargeWithCardToken(payload);
			if (response.HasError)
			{
				throw new Exception(response.Error.Message);
			}
			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", response.HttpStatusCode));
			}
			var charge = response.Model;
			Session["CreditCard.Charge"] = charge;

			// TODO: Check payment was authorised before capturing.

			// Capture
			var captureResponse = client.CaptureCharge(charge.Id, new ChargeCapture()
			{
				// I don't see the point in sending this info??
				Value = charge.Value,
				Description = charge.Description,
				Products = charge.Products
			});
			if (captureResponse.HasError)
			{
				throw new Exception(captureResponse.Error.Message);
			}
			if (captureResponse.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", captureResponse.HttpStatusCode));
			}
			var capture = captureResponse.Model;
			Session["CreditCard.Capture"] = capture;

			return View("Confirmation");
		}

		//public static APIClient CreateAPIClient()
		//{
		//	var secretKey = ConfigurationManager.AppSettings["Checkout.SecretKey"];
		//	var env = (Checkout.Helpers.Environment)Enum.Parse(typeof(Checkout.Helpers.Environment), ConfigurationManager.AppSettings["Checkout.Environment"], true);
		//	var debugMode = Convert.ToBoolean(ConfigurationManager.AppSettings["Checkout.DebugMode"]);
		//	var connectTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["Checkout.RequestTimeout"]);
		//	var client = new APIClient(secretKey, env, debugMode, connectTimeout);
		//	return client;
		//}

		public static CheckoutApiRestClient CreateAPIClient()
		{
			var secretKey = ConfigurationManager.AppSettings["Checkout.SecretKey"];
			var env = (Checkout.Helpers.Environment)Enum.Parse(typeof(Checkout.Helpers.Environment), ConfigurationManager.AppSettings["Checkout.Environment"], true);
			//var debugMode = Convert.ToBoolean(ConfigurationManager.AppSettings["Checkout.DebugMode"]); just writes debug messages to console.
			var connectTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["Checkout.RequestTimeout"]);
			var client = new CheckoutApiRestClient(secretKey, env, connectTimeout);
			return client;
		}

		public static APIContext CreatePayPalAPIContext()
		{
			// https://developer.paypal.com/developer/applications/ > REST API apps
			// Create an app and update app settings with client id and secret.
			var clientId = ConfigurationManager.AppSettings["PayPal.ClientId"];
			var secret = ConfigurationManager.AppSettings["PayPal.Secret"];
			var accessToken = new OAuthTokenCredential(clientId, secret).GetAccessToken();
			var context = new APIContext(accessToken);
			return context;
		}

		[HttpGet]
		[ActionName("PayPal")]
		public ActionResult GetPayPal(string method)
		{
			if (method == "Checkout")
				return PayPalCheckout();
			else
				return PayPalREST();
		}

		private ActionResult PayPalCheckout()
		{
			// https://docs.checkout.com/getting-started/merchant-api/alternative-payments/paypal/authorise-paypal-charge

			// Step 1: Create a Payment Token
			var shippingAddress = CreateCheckoutAddress();
			var billingAddress = CreateCheckoutAddress();
			var product = CreateCheckoutProduct();
			var orderId = Guid.NewGuid();
			var email = "cannonm@freshegg.com";

			var amount = ((product.Price * product.Quantity) + product.ShippingCost);

			var paymentTokenCreate = new PaymentTokenCreate()
			{
				Value = (amount * 100).ToString("0"),
				Currency = "GBP",
				AutoCapture = "N", // In addition, autoCapture must be set to n to capture authorised charges manually as can be seen in our Instant Settlement guide.
				ChargeMode = 3, // chargeMode must be set to 3 for all Alternative Payments.
				Email = email,
				//CustomerIp = Request.ServerVariables["REMOTE_ADDR"] ?? Request.ServerVariables["HTTP_X_FORWARDED_FOR"],
				TrackId = orderId.ToString(), // The trackId parameter is required when creating a payment token to be used with PayPal and should be unique for each request. 
				Description = "Order",
				//ShippingDetails = shippingAddress,
				// billing address??
				//Products = new List<Product>() { product }
			};

			var client = CreateAPIClient();
			var paymentTokenResponse = client.CreatePaymentToken(paymentTokenCreate);
			if (paymentTokenResponse.HasError)
			{
				throw new Exception(paymentTokenResponse.Error.Message);
			}
			if (paymentTokenResponse.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", paymentTokenResponse.HttpStatusCode));
			}
			var paymentToken = paymentTokenResponse.Model;
			Session["PayPal.PaymentToken"] = paymentToken;

			// Step 2: Create an Alternative Payment Charge
			var localPaymentChargeRequest = new LocalPaymentCharge()
			{
				Email = email,
				LocalPayment = new LocalPaymentCreate()
				{
					LppId = "lpp_19", // PayPal - https://docs.checkout.com/reference/checkout-js-reference/alternative-payments
					UserData = new Dictionary<string, string>()
				},
				PaymentToken = paymentToken.Id
			};
			var alternativePaymentChargeResponse = client.ChargeWithLocalPayment(localPaymentChargeRequest);
			if (alternativePaymentChargeResponse.HasError)
			{
				throw new Exception(alternativePaymentChargeResponse.Error.Message);
			}
			if (alternativePaymentChargeResponse.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", alternativePaymentChargeResponse.HttpStatusCode));
			}
			var charge = alternativePaymentChargeResponse.Model;

			// Step 3: Handle Alternative Payment Response
			if (charge.ResponseCode != "10000")
				throw new Exception("Unexpected response code creating charge: " + charge.ResponseCode);
			if (charge.LocalPayment == null || string.IsNullOrWhiteSpace(charge.LocalPayment.PaymentUrl))
				throw new Exception("No payment url for alternative charge: " + charge.ResponseCode);

			Session["PayPal.LocalPaymentCharge"] = charge;

			return Redirect(charge.LocalPayment.PaymentUrl);
		}

		// Step 4: Customer Completes Alternative Payment Page

		// Step 5: Checkout.com Redirects to Merchant's 'Payment Successful' Page
		// Please send your desired 'Payment Successful' and 'Payment Unsuccessful' redirect URLs to our integration team to configure. BIT NAFF.

		// Step 6: Verify the Alternative Payment Charge

		// Step 7: Confirm Payment via Webhooks
		// Webhooks are the only way to confirm the successful completion of an Alternative Payment transaction.

		private ActionResult PayPalREST()
		{
			// https://github.com/pedropaf/paypal-integration
			// http://paypal.github.io/PayPal-NET-SDK/Samples/PaymentWithPayPal.aspx.html
			var context = CreatePayPalAPIContext();

			var orderId = Guid.NewGuid();
			var baseUrl = Request.Url.Scheme + "://" + Request.Url.Host;
			if (!Request.Url.IsDefaultPort)
				baseUrl += ":" + Request.Url.Port.ToString();

			var transaction = new Transaction()
			{
				description = "sale",
				invoice_number = orderId.ToString(),
				amount = new Amount()
				{
					currency = "GBP",
					total = "44.50",
					details = new Details() // Details: Let's you specify details of a payment amount.
					{
						tax = "0.00",
						shipping = "4.50",
						subtotal = "40.00"
					}
				},
				item_list = new ItemList()
				{
					items = new List<Item>()
					{
						new Item()
						{
							name = "Kettle",
							currency = "GBP",
							price = "40.00",
							quantity = "1",
							sku = "KTL123"
						}
					}
				}
			};

			var payment = new Payment()
			{
				intent = "sale", // sale or authorize
				payer = new Payer() { payment_method = "paypal" },
				transactions = new List<Transaction>() { transaction },
				redirect_urls = new RedirectUrls()
				{
					cancel_url = baseUrl + "/Home/PaymentCancelled?orderId=" + orderId.ToString(),
					return_url = baseUrl + "/Home/PayPalPaymentSuccessful?orderId=" + orderId.ToString()
				}
			};

			// Create a payment using a valid APIContext
			var createdPayment = Payment.Create(context, payment);

			return Redirect(createdPayment.GetApprovalUrl());
		}

		public ActionResult PaymentCancelled([Bind(Prefix = "cko-payment-token")]string cardToken)
		{
			var paymentToken = Session["PayPal.PaymentToken"] as Checkout.ApiServices.Tokens.ResponseModels.PaymentToken;
			var charge = Session["PayPal.LocalPaymentCharge"] as Checkout.ApiServices.Charges.ResponseModels.Charge;

			// TODO: Handle cancelled payment
			return View();
		}

		public ActionResult PaymentSuccessful([Bind(Prefix = "cko-payment-token")]string paymentToken)
		{
			//var paymentToken = Session["PayPal.PaymentToken"] as Checkout.ApiServices.Tokens.ResponseModels.PaymentToken;
			//var charge = Session["PayPal.LocalPaymentCharge"] as Checkout.ApiServices.Charges.ResponseModels.Charge;

			if (!string.IsNullOrWhiteSpace(paymentToken))
			{
				var client = CreateAPIClient();

				// Verify the charge
				var chargeResponse = client.VerifyCharge(paymentToken);
				if (chargeResponse.HasError)
					throw new Exception(chargeResponse.Error.Message);
				if (chargeResponse.HttpStatusCode != HttpStatusCode.OK)
					throw new Exception(string.Format("Failed with status code: {0}", chargeResponse.HttpStatusCode));
				var charge = chargeResponse.Model;

				// Capture the charge
				var captureResponse = client.CaptureCharge(charge.Id, new ChargeCapture()
				{
					// I don't see the point in sending this info??
					Value = charge.Value,
					Description = charge.Description,
					Products = charge.Products
				});
				if (captureResponse.HasError)
				{
					throw new Exception(captureResponse.Error.Message);
				}
				if (captureResponse.HttpStatusCode != HttpStatusCode.OK)
				{
					throw new Exception(string.Format("Failed with status code: {0}", captureResponse.HttpStatusCode));
				}
				var capture = captureResponse.Model;
				Session["PayPal.Capture"] = capture;
			}

			return View(new { });
		}

		[ActionName("PayPalPaymentSuccessful")]
		public ActionResult PaymentSuccessful(string paymentId, string token, string payerId)
		{
			var context = CreatePayPalAPIContext();

			var paymentExecution = new PaymentExecution() { payer_id = payerId };
			var payment = new Payment() { id = paymentId };
			var executedPayment = payment.Execute(context, paymentExecution);

			Session["PayPal.Payment"] = executedPayment;

			return View(executedPayment);
		}

		public ActionResult RefundCreditCard()
		{
			if (Session["CreditCard.Capture"] != null)
				_RefundCreditCard();
			else if (Session["CreditCard.Charge"] != null)
				_VoidCreditCard();

			return RedirectToAction("Index");
		}

		public void _RefundCreditCard()
		{
			var capture = Session["CreditCard.Capture"] as Checkout.ApiServices.Charges.ResponseModels.Capture;
			if (capture == null || capture.Status != "Captured")
				return;

			// Captured, so refund
			var client = CreateAPIClient();
			var response = client.RefundCharge(capture.Id, new ChargeRefund()
			{
				// I don't see the point in sending this info??
				//Value = capture.Value,
				Description = capture.Description,
				Products = capture.Products,
			});

			if (response.HasError)
				throw new Exception(response.Error.Message);
			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", response.HttpStatusCode));
			}

			var refund = response.Model;
			if (refund.Status != "Refunded")
				throw new Exception("Not refunded??");

			Session["CreditCard.Refund"] = refund;
		}

		public void _VoidCreditCard()
		{
			var charge = Session["CreditCard.Charge"] as Checkout.ApiServices.Charges.ResponseModels.Charge;
			if (charge == null || charge.Status != "Authorised")
				return;

			// Authorised, so void
			var client = CreateAPIClient();
			var response = client.VoidCharge(charge.Id, new ChargeVoid()
			{
				// I don't see the point in sending this info??
				Description = charge.Description,
				Products = charge.Products,
			});

			if (response.HasError)
				throw new Exception(response.Error.Message);
			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new Exception(string.Format("Failed with status code: {0}", response.HttpStatusCode));
			}

			var @void = response.Model;
			if (@void.Status != "Voided")
				throw new Exception("Not voided??");

			Session["CreditCard.Void"] = @void;
		}

		public ActionResult RefundCreditCardWithId(string chargeId)
		{
			if (string.IsNullOrWhiteSpace(chargeId))
				return RedirectToAction("Index");

			var client = CreateAPIClient();

			var chargeResponse = client.GetCharge(chargeId);
			if (chargeResponse.HasError)
				throw new Exception(chargeResponse.Error.Message);
			if (chargeResponse.HttpStatusCode != HttpStatusCode.OK)
				throw new Exception(string.Format("Failed with status code: {0}", chargeResponse.HttpStatusCode));
			var charge = chargeResponse.Model;

			if (charge.Status == "Captured")
			{
				var response = client.RefundCharge(chargeId, new ChargeRefund()
				{
					Value = charge.Value
				});
				if (response.HasError)
					throw new Exception(response.Error.Message);
				if (response.HttpStatusCode != HttpStatusCode.OK)
				{
					throw new Exception(string.Format("Failed with status code: {0}", response.HttpStatusCode));
				}
				var refund = response.Model;

				if (refund.Status != "Refunded")
					throw new Exception("Not refunded??");
				Session["CreditCard.Refund"] = refund;
			}
			else if (charge.Status == "Authorised")
			{
				var response = client.VoidCharge(charge.Id, new ChargeVoid()
				{
					//// I don't see the point in sending this info??
					//Description = charge.Description,
					//Products = charge.Products,
				});

				if (response.HasError)
					throw new Exception(response.Error.Message);
				if (response.HttpStatusCode != HttpStatusCode.OK)
				{
					throw new Exception(string.Format("Failed with status code: {0}", response.HttpStatusCode));
				}

				var @void = response.Model;
				if (@void.Status != "Voided")
					throw new Exception("Not voided??");

				Session["CreditCard.Void"] = @void;
			}
			else if (charge.Status == "Pending")
			{
				// Pending - don't do anything.
			}

			return RedirectToAction("Index");
		}

		public ActionResult RefundPayPal()
		{
			var payment = Session["PayPal.Payment"] as Payment;
			if (payment == null)
				return RedirectToAction("Index");

			// Get the sale resource from the executed payment's list of related resources.
			var tx = payment.transactions[0];
			var sale = tx.related_resources[0].sale;

			var context = CreatePayPalAPIContext();
			var response = Sale.Refund(context, sale.id, new RefundRequest()
			{
				amount = tx.amount,
				description = tx.description,
				reason = "Don't want it",
			});

			if (response.state != "completed")
				throw new Exception("state is not completed??");

			Session["PayPal.Refund"] = response;
			return RedirectToAction("Index");
		}

		public ActionResult RefundPayPalWithSaleId(string saleId)
		{
			if (string.IsNullOrWhiteSpace(saleId))
				return RedirectToAction("Index");

			var context = CreatePayPalAPIContext();
			var response = Sale.Refund(context, saleId, new RefundRequest()
			{
			});

			if (response.state != "completed")
				throw new Exception("state is not completed??");

			Session["PayPal.Refund"] = response;
			return RedirectToAction("Index");
		}

		[HttpPost]
		[CheckoutWebhookAuthorize]
		public ActionResult CheckoutWebhook()
		{
			string json;
			Request.InputStream.Seek(0, System.IO.SeekOrigin.Begin);
			using (var inputStream = new System.IO.StreamReader(Request.InputStream))
			{
				json = inputStream.ReadToEnd();
			}
			var evt = JsonConvert.DeserializeObject<dynamic>(json);

			var hooks = MemoryCache.Default["Hooks"] as Dictionary<string, object> ?? new Dictionary<string, object>();
			hooks.Add(string.Format("{0} {1}", evt.eventType, evt.message.id), evt);
			MemoryCache.Default["Hooks"] = hooks;
			return new HttpStatusCodeResult(HttpStatusCode.OK);
		}

		[HttpPost]
		[PayPalWebhookAuthorize]
		public ActionResult PayPalWebhook()
		{
			string json;
			Request.InputStream.Seek(0, System.IO.SeekOrigin.Begin);
			using (var inputStream = new System.IO.StreamReader(Request.InputStream))
			{
				json = inputStream.ReadToEnd();
			}

			// https://developer.paypal.com/docs/integration/direct/webhooks/notification-messages/
			var evt = JsonConvert.DeserializeObject<dynamic>(json);
			var hooks = MemoryCache.Default["Hooks"] as Dictionary<string, object> ?? new Dictionary<string, object>();
			hooks.Add(string.Format("{0} {1}", evt.event_type, evt.resource_type), evt);
			MemoryCache.Default["Hooks"] = hooks;

			return new HttpStatusCodeResult(HttpStatusCode.OK);
		}
	}

	public class PayPalWebhookAuthorizeAttribute : AuthorizeAttribute
	{
		public override void OnAuthorization(AuthorizationContext filterContext)
		{
			if (Authorize(filterContext))
			{
				return;
			}
			HandleUnauthorizedRequest(filterContext);
		}
		protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
		{
			base.HandleUnauthorizedRequest(filterContext);
		}
		private bool Authorize(AuthorizationContext actionContext)
		{
			try
			{
				// https://developer.paypal.com/docs/integration/direct/webhooks/notification-messages/ > Event headers
				HttpRequestBase request = actionContext.RequestContext.HttpContext.Request;

				// https://github.com/paypal/PayPal-NET-SDK/wiki/Webhook-Event-Validation
				var context = HomeController.CreatePayPalAPIContext();

				// Use the webhook ID given when you set up the webhook url in the application.
				var webhookId = ConfigurationManager.AppSettings["PayPal.WebhookId"];

				var requestheaders = HttpContext.Current.Request.Headers;

				string requestBody;
				request.InputStream.Seek(0, System.IO.SeekOrigin.Begin);
				using (var reader = new StreamReader(request.InputStream, Encoding.UTF8, true, 1024, true))
				{
					requestBody = reader.ReadToEnd();
				}
				//var bytes = new byte[request.InputStream.Length];
				//request.InputStream.Read(bytes, 0, bytes.Length);
				//request.InputStream.Position = 0;
				//var requestBody = Encoding.UTF8.GetString(bytes);

				var isValid = WebhookEvent.ValidateReceivedEvent(context, requestheaders, requestBody, webhookId);
				return isValid;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public class CheckoutWebhookAuthorizeAttribute : AuthorizeAttribute
	{
		public override void OnAuthorization(AuthorizationContext filterContext)
		{
			if (Authorize(filterContext))
			{
				return;
			}
			HandleUnauthorizedRequest(filterContext);
		}
		protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
		{
			filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
			//base.HandleUnauthorizedRequest(filterContext);
		}
		private bool Authorize(AuthorizationContext actionContext)
		{
			try
			{
				HttpRequestBase request = actionContext.RequestContext.HttpContext.Request;
				string token = request.Headers["Authorization"];
				return IsTokenValid(token, GetIP(request), request.UserAgent);
			}
			catch (Exception)
			{
				return false;
			}
		}
		public static bool IsTokenValid(string token, string ip, string userAgent)
		{
			// Check the originating IP
			// List of IPs here: https://docs.checkout.com/getting-started/webhooks
			var ips = ConfigurationManager.AppSettings["Checkout.WebhookIPs"].Split(new char[] { ';' });
			if (!ips.Contains(ip))
				return false;

			// Validate the token using the secret key. Straight comparison.
			var secretKey = ConfigurationManager.AppSettings["Checkout.WebhookKey"];
			if (!string.Equals(secretKey, token))
				return false;

			return true;
		}
		public static string GetIP(HttpRequestBase request)
		{
			string ip = request.Headers["X-Forwarded-For"];

			if (!string.IsNullOrWhiteSpace(ip) && ip.Contains(","))
			{
				// if this is a list of IPs then choose the last one.
				ip = ip.Split(new[] { ',' }).Last().Trim();
			}

			if (string.IsNullOrEmpty(ip))
			{
				ip = request.UserHostAddress;
			}

			return ip;
		}
	}

	//public class TokenBinInfo
	//{
	//	//{
	//	//  "token": "card_tok_AF377225-9A51-4A33-B9F8-B75F9D14D709",
	//	//  "bin": "549486",
	//	//  "issuer": "ALANDSBANKEN ABP",
	//	//  "issuerCountry": "Finland",
	//	//  "issuerCountryIso2": "FI",
	//	//  "scheme": "Visa",
	//	//  "type": "Credit",
	//	//  "category": "Consumer",
	//	//  "productId": "F",
	//	//  "productType": "CLASSIC"
	//	//}
	//	public string Token { get; set; }
	//	public string Bin { get; set; }
	//	public string Issuer { get; set; }
	//	public string IssuerCountry { get; set; }
	//	public string IssuerCountryISO2 { get; set; }
	//	public string Scheme { get; set; }
	//	public string Type { get; set; }
	//	public string Category { get; set; }
	//	public string ProductId { get; set; }
	//	public string ProductType { get; set; }
	//}

	//public static class TokenServiceExtensions
	//{
	//	public static HttpResponse<TokenBinInfo> GetBinLookupViaCardToken(this TokenService tokenService, string cardToken)
	//	{
	//		// https://docs.checkout.com/reference/merchant-api-reference/lookups/bin-lookup-via-card-token
	//		// SDK doesn't support this endpoint so call it manually.
	//		var uri = string.Concat(AppSettings.BaseApiUri, string.Format("/tokens/{0}", cardToken));
	//		return new ApiHttpClient().GetRequest<TokenBinInfo>(uri, AppSettings.SecretKey);
	//	}
	//}
}