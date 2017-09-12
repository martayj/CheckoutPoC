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

		public ActionResult CreditCard()
		{
			return View();
		}

		//public class CheckoutKitResponse
		//{
		//	//"id": "card_tok_22008D7D-B198-4D62-9970-E03476933162",
		//	[JsonProperty("id")]
		//	public string Id { get; set; }

		//	//"liveMode": "false",
		//	[JsonProperty("liveMode")]
		//	public bool LiveMode { get; set; }

		//	//"created": "2016-10-17T12:30:02Z",
		//	[JsonProperty("created")]
		//	public DateTime Created { get; set; }

		//	//"used": false,
		//	[JsonProperty("used")]
		//	public bool Used { get; set; }

		//	public class CheckoutKitResponseCard
		//	{
		//		//"expiryMonth": "06",
		//		[JsonProperty("expiryMonth")]
		//		public string ExpiryMonth { get; set; }

		//		//"expiryYear": "2018",
		//		[JsonProperty("expiryYear")]
		//		public string ExpiryYear { get; set; }

		//		//"last4": "8845",
		//		[JsonProperty("last4")]
		//		public string Last4 { get; set; }

		//		//"bin": "488065",
		//		[JsonProperty("bin")]
		//		public string Bin { get; set; }

		//		//"paymentMethod": "Visa"
		//		[JsonProperty("paymentMethod")]
		//		public string PaymentMethod { get; set; }
		//	}

		//	//"card": {
		//	//},
		//	[JsonProperty("card")]
		//	public CheckoutKitResponseCard Card { get; set; }

		//	public class CheckoutKitResponseBinData
		//	{
		//		//"bin": "488065",
		//		[JsonProperty("bin")]
		//		public string Bin { get; set; }

		//		//"cardType": "Debit",
		//		[JsonProperty("cardType")]
		//		public string CardType { get; set; }

		//		//"countryName": "Netherlands",
		//		[JsonProperty("countryName")]
		//		public string CountryName { get; set; }

		//		//"bankName": "ING Bank",
		//		[JsonProperty("bankName")]
		//		public string BankName { get; set; }

		//		//"issuerCountryISO2": "NL"
		//		[JsonProperty("issuerCountryISO2")]
		//		public string IssuerCountryISO2 { get; set; }
		//	}

		//	//"binData": {
		//	//}
		//	[JsonProperty("binData")]
		//	public CheckoutKitResponseBinData BinData { get; set; }
		//}

		[HttpPost]
		public ActionResult CreditCard([Bind(Prefix = "cko-card-token")]string cardToken)
		{
			// Create payload
			// https://docs.checkout.com/reference/merchant-api-reference/charges/charge-with-card-token

			var shippingAddress = new Checkout.ApiServices.SharedModels.Address()
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

			var billingAddress = new Checkout.ApiServices.SharedModels.Address()
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

			var product = new Product()
			{
				Name = "Kettle",
				Description = "It's a kettle",
				Price = 40m,
				Quantity = 1,
				ShippingCost = 4.50m,
				Sku = "1aab2aa",
				TrackingUrl = null
			};

			var payload = new CardTokenCharge()
			{
				//AutoCapture = "Y", // transfer funds automatically
				//AutoCapTime = 0, // transfer funds immediately after authorisation
				AutoCapture = "N", // manually capture
				ChargeMode = 1, // non-3D
				Email = "martin.cannon@freshegg.com",
				Description = "Order",
				Value = (((product.Price * product.Quantity) + product.ShippingCost) * 100).ToString("0"),
				Currency = "GBP",
				//TrackId = "TRK12345",
				TransactionIndicator = "1", // regular
				CustomerIp = Request.ServerVariables["REMOTE_ADDR"] ?? Request.ServerVariables["HTTP_X_FORWARDED_FOR"],
				CardToken = cardToken,
				ShippingDetails = shippingAddress,
				// billing address??
				Products = new List<Product>() { product }
			};

			var client = CreateAPIClient();
			// Authorise
			var response = client.ChargeService.ChargeWithCardToken(payload);
			if (response.HasError)
			{
				throw new Exception(response.Error.Message);
			}
			var charge = response.Model;
			Session["CreditCard.Charge"] = charge;

			// Capture
			var captureResponse = client.ChargeService.CaptureCharge(charge.Id, new ChargeCapture()
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
			var capture = captureResponse.Model;
			Session["CreditCard.Capture"] = capture;

			return View("Confirmation");
		}

		public static APIClient CreateAPIClient()
		{
			var secretKey = ConfigurationManager.AppSettings["Checkout.SecretKey"];
			var env = (Checkout.Helpers.Environment)Enum.Parse(typeof(Checkout.Helpers.Environment), ConfigurationManager.AppSettings["Checkout.Environment"], true);
			var debugMode = Convert.ToBoolean(ConfigurationManager.AppSettings["Checkout.DebugMode"]);
			var connectTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["Checkout.RequestTimeout"]);
			var client = new APIClient(secretKey, env, debugMode, connectTimeout);
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

		public ActionResult PayPal()
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
					return_url = baseUrl + "/Home/PaymentSuccessful?orderId=" + orderId.ToString()
				}
			};

			// Create a payment using a valid APIContext
			var createdPayment = Payment.Create(context, payment);

			return Redirect(createdPayment.GetApprovalUrl());
		}

		public ActionResult PaymentCancelled()
		{
			// TODO: Handle cancelled payment
			return View();
		}

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
			else if (Session["CreditCard.Capture"] != null)
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
			var response = client.ChargeService.RefundCharge(capture.Id, new ChargeRefund()
			{
				// I don't see the point in sending this info??
				//Value = capture.Value,
				Description = capture.Description,
				Products = capture.Products,
			});

			if (response.HasError)
				throw new Exception(response.Error.Message);

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
			var response = client.ChargeService.VoidCharge(charge.Id, new ChargeVoid()
			{
				// I don't see the point in sending this info??
				Description = charge.Description,
				Products = charge.Products,
			});

			if (response.HasError)
				throw new Exception(response.Error.Message);

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
			var response = client.ChargeService.RefundCharge("charge_" + chargeId, new ChargeRefund()
			{
			});

			if (response.HasError)
				throw new Exception(response.Error.Message);

			var refund = response.Model;
			if (refund.Status != "Refunded")
				throw new Exception("Not refunded??");

			Session["CreditCard.Refund"] = refund;
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
			base.HandleUnauthorizedRequest(filterContext);
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

			if (string.IsNullOrEmpty(ip))
			{
				ip = request.UserHostAddress;
			}

			return ip;
		}
	}
}