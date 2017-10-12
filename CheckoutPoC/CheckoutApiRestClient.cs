namespace CheckoutPoC
{
	using Checkout;
	using Checkout.ApiServices.Charges.RequestModels;
	using Checkout.ApiServices.Charges.ResponseModels;
	using Checkout.ApiServices.SharedModels;
	using Checkout.ApiServices.Tokens.RequestModels;
	using Checkout.ApiServices.Tokens.ResponseModels;
	using Checkout.Helpers;
	using Newtonsoft.Json;
	using RestSharp;

	public class CheckoutApiRestClient
	{
		public static class Endpoints
		{
			public const string Charges = "/charges";
			public const string Charge = "/charges/{chargeId}";
			public const string ChargeHistory = "/charges/{chargeId}/history";
			public const string CaptureCharge = "/charges/{chargeId}/capture";
			public const string RefundCharge = "/charges/{chargeId}/refund";
			public const string VoidCharge = "/charges/{chargeId}/void";
			public const string DefaultCardCharge = "/charges/customer";
			public const string LocalPaymentCharge = "/charges/localpayment";
			public const string CardTokenCharge = "/charges/token";
			public const string CardCharge = "/charges/card";
			public const string CardToken = "/tokens/card";
			public const string PaymentToken = "/tokens/payment";
			public const string UpdatePaymentToken = "/tokens/payment/{paymentToken}";
			public const string VisaCheckout = "/tokens/card/visa-checkout";
			public const string Customers = "/customers";
			public const string Customer = "/customers/{0}";
			public const string Cards = "/customers/{0}/cards";
			public const string Card = "/customers/{0}/cards/{1}";
			public const string ReportingTransactions = "/reporting/transactions";
			public const string ReportingChargebacks = "/reporting/chargebacks";
			public const string BinLookup = "/lookups/bins/{0}";
			public const string LocalPaymentIssuerIdLookup = "/lookups/localpayments/{0}/tags/issuerid";
			public const string RecurringPaymentPlans = "/recurringPayments/plans";
			public const string RecurringPaymentPlan = "/recurringPayments/plans/{0}";
			public const string RecurringPaymentPlanSearch = "/recurringPayments/plans/search";
			public const string RecurringCustomerPaymentPlanSearch = "/recurringPayments/customers/search";
			public const string RecurringCustomerPaymentPlan = "/recurringPayments/customers/{0}";
		}

		public CheckoutApiRestClient(string secretKey, Environment env, int connectTimeout)
		{
			AppSettings.RequestTimeout = connectTimeout;
			AppSettings.SecretKey = secretKey;
			AppSettings.Environment = env; // setting the environment sets the base uri to sandbox/live.
			ContentAdaptor.Setup();
		}

		#region Token service
		public HttpResponse<PaymentToken> CreatePaymentToken(PaymentTokenCreate requestModel)
		{
			var request = new RestRequest(Endpoints.PaymentToken, Method.POST);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<PaymentToken>(request);
		}

		public HttpResponse<OkResponse> UpdatePaymentToken(string paymentToken, PaymentTokenUpdate requestModel)
		{
			var request = new RestRequest(Endpoints.UpdatePaymentToken, Method.PUT);
			request.AddUrlSegment("paymentToken", paymentToken);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<OkResponse>(request);
		}

		// needs public key. we won't need this endpoint.
		//public HttpResponse<CardTokenResponse> CreateVisaCheckoutCardToken(VisaCheckoutTokenCreate requestModel)
		//{
		//	var request = new RestRequest(Endpoints.VisaCheckout, Method.POST);
		//	return Execute<CardTokenResponse>(request);
		//	return new ApiHttpClient().PostRequest<CardTokenResponse>(ApiUrls.VisaCheckout, AppSettings.PublicKey, requestModel);
		//}

		public HttpResponse<TokenBinInfo> GetBinLookupViaCardToken(string cardToken)
		{
			// https://docs.checkout.com/reference/merchant-api-reference/lookups/bin-lookup-via-card-token
			// SDK doesn't support this endpoint so call it manually.
			var request = new RestRequest("/tokens/{cardToken}", Method.GET);
			request.AddUrlSegment("cardToken", cardToken);
			return Execute<TokenBinInfo>(request);
		}

		#endregion Token service

		#region Charge Service
		/// <summary>
		/// Creates a charge with full card details.
		/// </summary>
		public HttpResponse<Charge> ChargeWithCard(CardCharge requestModel)
		{
			var request = new RestRequest(Endpoints.CardCharge, Method.POST);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Creates a charge with card id.
		/// </summary>
		public HttpResponse<Charge> ChargeWithCardId(CardIdCharge requestModel)
		{
			var request = new RestRequest(Endpoints.CardCharge, Method.POST);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Creates a charge with card token.
		/// </summary>
		public HttpResponse<Charge> ChargeWithCardToken(CardTokenCharge requestModel)
		{
			var request = new RestRequest(Endpoints.CardTokenCharge, Method.POST);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Creates a charge with the default card of the customer.
		/// </summary>
		public HttpResponse<Charge> ChargeWithDefaultCustomerCard(DefaultCardCharge requestModel)
		{
			var request = new RestRequest(Endpoints.DefaultCardCharge, Method.POST);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Creates a charge with an alternative/local payment.
		/// </summary>
		/// <param name="requestModel">The request model.</param>
		/// <returns></returns>
		public HttpResponse<Charge> ChargeWithLocalPayment(LocalPaymentCharge requestModel)
		{
			var request = new RestRequest(Endpoints.LocalPaymentCharge, Method.POST);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Voids an authorised charge. If charge has been captured you cannot perform void operation.
		/// </summary>
		public HttpResponse<Void> VoidCharge(string chargeId, ChargeVoid requestModel)
		{
			if (!chargeId.StartsWith("charge_"))
				chargeId = "charge_" + chargeId;

			var request = new RestRequest(Endpoints.VoidCharge, Method.POST);
			request.AddUrlSegment("chargeId", chargeId);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Void>(request);
		}

		/// <summary>
		/// Refunds a captured charge.
		/// </summary>
		public HttpResponse<Refund> RefundCharge(string chargeId, ChargeRefund requestModel)
		{
			if (!chargeId.StartsWith("charge_"))
				chargeId = "charge_" + chargeId;

			var request = new RestRequest(Endpoints.RefundCharge, Method.POST);
			request.AddUrlSegment("chargeId", chargeId);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Refund>(request);
		}

		/// <summary>
		/// Captures an authorised charge.
		/// </summary>
		public HttpResponse<Capture> CaptureCharge(string chargeId, ChargeCapture requestModel)
		{
			if (!chargeId.StartsWith("charge_"))
				chargeId = "charge_" + chargeId;

			var request = new RestRequest(Endpoints.CaptureCharge, Method.POST);
			request.AddUrlSegment("chargeId", chargeId);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<Capture>(request);
		}

		/// <summary>
		/// Updates a charge.
		/// </summary>
		public HttpResponse<OkResponse> UpdateCharge(string chargeId, ChargeUpdate requestModel)
		{
			if (!chargeId.StartsWith("charge_"))
				chargeId = "charge_" + chargeId;

			var request = new RestRequest(Endpoints.Charge, Method.PUT);
			request.AddUrlSegment("chargeId", chargeId);
			request.RequestFormat = DataFormat.Json;
			request.AddBody(requestModel);
			return Execute<OkResponse>(request);
		}

		/// <summary>
		/// Retrieves a charge by chargeId
		/// </summary>
		public HttpResponse<Charge> GetCharge(string chargeId)
		{
			if (!chargeId.StartsWith("charge_"))
				chargeId = "charge_" + chargeId;

			var request = new RestRequest(Endpoints.Charge, Method.GET);
			request.AddUrlSegment("chargeId", chargeId);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Verify a charge by paymentToken (or chargeId)
		/// </summary>
		public HttpResponse<Charge> VerifyCharge(string paymentToken)
		{
			var request = new RestRequest(Endpoints.Charge, Method.GET);
			request.AddUrlSegment("chargeId", paymentToken);
			return Execute<Charge>(request);
		}

		/// <summary>
		/// Retrieves charge history by chargeId
		/// </summary>
		public HttpResponse<ChargeHistory> GetChargeHistory(string chargeId)
		{
			if (!chargeId.StartsWith("charge_"))
				chargeId = "charge_" + chargeId;

			var request = new RestRequest(Endpoints.ChargeHistory, Method.GET);
			request.AddUrlSegment("chargeId", chargeId);
			return Execute<ChargeHistory>(request);
		}
		#endregion Charge Service

		public HttpResponse<T> Execute<T>(RestRequest request) where T : new()
		{
			request.AddHeader("Authorization", AppSettings.SecretKey);
			request.AddHeader("Accept-Encoding", "Gzip");

			var client = new RestClient();
			client.BaseUrl = new System.Uri(AppSettings.BaseApiUri);
			client.Timeout = System.Convert.ToInt32(System.TimeSpan.FromSeconds(AppSettings.RequestTimeout).TotalMilliseconds);
			client.UserAgent = AppSettings.ClientUserAgentName;
			var response = client.Execute<T>(request);

			if (response.ErrorException != null)
			{
				return new HttpResponse<T>(default(T))
				{
					Error = JsonConvert.DeserializeObject<ResponseError>(response.Content),
					HttpStatusCode = response.StatusCode
				};
			}
			else
			{
				return new HttpResponse<T>(response.Data)
				{
					HttpStatusCode = response.StatusCode
				};
			}
		}
	}

	public class TokenBinInfo
	{
		//{
		//  "token": "card_tok_AF377225-9A51-4A33-B9F8-B75F9D14D709",
		//  "bin": "549486",
		//  "issuer": "ALANDSBANKEN ABP",
		//  "issuerCountry": "Finland",
		//  "issuerCountryIso2": "FI",
		//  "scheme": "Visa",
		//  "type": "Credit",
		//  "category": "Consumer",
		//  "productId": "F",
		//  "productType": "CLASSIC"
		//}
		public string Token { get; set; }
		public string Bin { get; set; }
		public string Issuer { get; set; }
		public string IssuerCountry { get; set; }
		public string IssuerCountryISO2 { get; set; }
		public string Scheme { get; set; }
		public string Type { get; set; }
		public string Category { get; set; }
		public string ProductId { get; set; }
		public string ProductType { get; set; }
	}
}