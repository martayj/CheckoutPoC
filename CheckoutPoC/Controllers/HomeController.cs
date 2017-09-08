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

namespace CheckoutComAndPayPalPoC.Controllers
{
	public class HomeController : Controller
	{
		public ActionResult Index()
		{
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
				AutoCapTime = 24,
				AutoCapture = "Y",
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
				Products = new List<Product>() { product },
			};

			var client = CreateAPIClient();
			var response = client.ChargeService.ChargeWithCardToken(payload);
			if (response.HasError)
			{
				throw new Exception(response.Error.Message);
			}
			var charge = response.Model;

			return View("Confirmation", charge);
		}

		private APIClient CreateAPIClient()
		{
			var secretKey = ConfigurationManager.AppSettings["Checkout.SecretKey"];
			var env = (Checkout.Helpers.Environment)Enum.Parse(typeof(Checkout.Helpers.Environment), ConfigurationManager.AppSettings["Checkout.Environment"], true);
			var debugMode = Convert.ToBoolean(ConfigurationManager.AppSettings["Checkout.DebugMode"]);
			var connectTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["Checkout.RequestTimeout"]);
			var client = new APIClient(secretKey, env, debugMode, connectTimeout);
			return client;
		}

		private APIContext CreatePayPalAPIContext()
		{
			var clientId = ConfigurationManager.AppSettings["PayPal.ClientId"];
			var secret = ConfigurationManager.AppSettings["PayPal.Secret"];
			var accessToken = new OAuthTokenCredential(clientId, secret).GetAccessToken();
			var context = new APIContext(accessToken);
			return context;
		}

		public ActionResult PayPal()
		{
			// https://github.com/pedropaf/paypal-integration
			var context = CreatePayPalAPIContext();

			var orderId = 1234;
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
			var createdPayment = payment.Create(context);

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
			return View(executedPayment);
		}
	}
}