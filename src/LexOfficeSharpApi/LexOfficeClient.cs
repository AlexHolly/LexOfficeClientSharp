﻿using AndreasReitberger.API.LexOffice.Enum;
using AndreasReitberger.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AndreasReitberger.API.LexOffice
{
    // https://developers.lexoffice.io/docs/#lexoffice-api-documentation/?cid=1766
    public partial class LexOfficeClient : ObservableObject
    {

        #region Instance
        static LexOfficeClient? _instance = null;
        static readonly object Lock = new();
        public static LexOfficeClient Instance
        {
            get
            {
                lock (Lock)
                {
                    _instance ??= new LexOfficeClient();
                }
                return _instance;
            }
            set
            {
                if (_instance == value) return;
                lock (Lock)
                {
                    _instance = value;
                }
            }
        }

        [ObservableProperty]
        bool isActive = false;

        [ObservableProperty]
        bool isInitialized = false;

        #endregion

        #region Static
        public static string HandlerName = "LexOffice";
        public static string HandlerLicenseUri = "https://www.lexoffice.de/public-api-lizenz-nutzungsbedingungen/";
        #endregion

        #region Variable
        RestClient? restClient;
        HttpClient? httpClient;

        const string _appBaseUrl = "https://api.lexoffice.io/";
        const string _apiVersion = "v1";

        JsonSerializerSettings jsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffK"
        };

        #endregion

        #region Properties

        #region General

        [ObservableProperty]
        [property: JsonIgnore, XmlIgnore]
        string? accessToken = null;
        partial void OnAccessTokenChanged(string? value) => VerifyAccessToken();

        [ObservableProperty]
        [property: JsonIgnore, XmlIgnore]
        bool isConnecting = false;
        [JsonIgnore, XmlIgnore]

        [ObservableProperty]
        [property: JsonIgnore, XmlIgnore]
        bool isOnline = false;

        [ObservableProperty]
        [property: JsonIgnore, XmlIgnore]
        bool isAccessTokenValid = false;

        [ObservableProperty]
        int defaultTimeout = 10000;

        #endregion

        #region Proxy

        [ObservableProperty]
        bool enableProxy = false;
        partial void OnEnableProxyChanged(bool value) => UpdateRestClientInstance();

        [ObservableProperty]
        bool proxyUseDefaultCredentials = true;
        partial void OnProxyUseDefaultCredentialsChanged(bool value) => UpdateRestClientInstance();

        [ObservableProperty]
        bool secureProxyConnection = true;
        partial void OnSecureProxyConnectionChanged(bool value) => UpdateRestClientInstance();

        [ObservableProperty]
        string proxyAddress = string.Empty;
        partial void OnProxyAddressChanged(string value) => UpdateRestClientInstance();

        [ObservableProperty]
        int proxyPort = 443;
        partial void OnProxyPortChanged(int value) => UpdateRestClientInstance();

        [ObservableProperty]
        string proxyUser = string.Empty;
        partial void OnProxyUserChanged(string value) => UpdateRestClientInstance();

        [ObservableProperty]
        [property: JsonIgnore, XmlIgnore]
        string? proxyPassword;
        partial void OnProxyPasswordChanged(string? value) => UpdateRestClientInstance();

        #endregion

        #endregion

        #region EventHandlers
        public event EventHandler? Error;
        protected virtual void OnError()
        {
            Error?.Invoke(this, EventArgs.Empty);
        }
        protected virtual void OnError(UnhandledExceptionEventArgs e)
        {
            Error?.Invoke(this, e);
        }
        #endregion

        #region Constructor
        public LexOfficeClient()
        {
            IsInitialized = false;
        }
        public LexOfficeClient(string accessToken)
        {
            AccessToken = accessToken;
            IsInitialized = true;
            Instance = this;
        }
        #endregion

        #region Methods
        void UpdateRestClientInstance()
        {
            if (string.IsNullOrEmpty(_appBaseUrl) || string.IsNullOrEmpty(_apiVersion))
            {
                return;
            }
            if (EnableProxy && !string.IsNullOrEmpty(ProxyAddress))
            {
                RestClientOptions options = new($"{_appBaseUrl}{_apiVersion}/")
                {
                    ThrowOnAnyError = true,
                    Timeout = TimeSpan.FromSeconds(DefaultTimeout / 1000),
                };
                HttpClientHandler httpHandler = new()
                {
                    UseProxy = true,
                    Proxy = GetCurrentProxy(),
                    AllowAutoRedirect = true,
                };

                httpClient = new(handler: httpHandler, disposeHandler: true);
                restClient = new(httpClient: httpClient, options: options);
            }
            else
            {
                httpClient = null;
                restClient = new(baseUrl: $"{_appBaseUrl}{_apiVersion}/");
            }
        }

        async Task<string?> BaseApiCallAsync(string command, Method method = Method.Get, string body = "", CancellationTokenSource? cts = default)
        {
            string? result = string.Empty;
            if (cts == default)
            {
                cts = new(DefaultTimeout);
            }
            if (restClient is null)
            {
                UpdateRestClientInstance();
            }

            RestRequest request = new(command, method)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddHeader("Authorization", $"Bearer {AccessToken}");
            request.AddJsonBody(body);
            if (restClient is not null)
            {
                RestResponse? respone = await restClient.ExecuteAsync(request, cts.Token).ConfigureAwait(false);
                if ((respone.StatusCode == HttpStatusCode.OK || respone.StatusCode == HttpStatusCode.Created) && respone.ResponseStatus == ResponseStatus.Completed)
                {
                    result = respone?.Content;
                }
            }
            return result;
        }

        void VerifyAccessToken()
        {
            try
            {
                IsAccessTokenValid = Regex.IsMatch(AccessToken ?? string.Empty, RegexHelper.LexOfficeAccessToken) && !string.IsNullOrEmpty(AccessToken);
            }
            catch(Exception exc)
            {
                IsAccessTokenValid = false;
                OnError(new UnhandledExceptionEventArgs(exc, false));
            }
        }
        #endregion

        #region Public Methods

        #region Proxy
        Uri GetProxyUri() => 
            ProxyAddress.StartsWith("http://") || ProxyAddress.StartsWith("https://") ? new Uri($"{ProxyAddress}:{ProxyPort}") : new Uri($"{(SecureProxyConnection ? "https" : "http")}://{ProxyAddress}:{ProxyPort}");
        
        WebProxy GetCurrentProxy()
        {
            WebProxy proxy = new()
            {
                Address = GetProxyUri(),
                BypassProxyOnLocal = false,
                UseDefaultCredentials = ProxyUseDefaultCredentials,
            };
            if (ProxyUseDefaultCredentials && !string.IsNullOrEmpty(ProxyUser))
            {
                proxy.Credentials = new NetworkCredential(ProxyUser, ProxyPassword);
            }
            else
            {
                proxy.UseDefaultCredentials = ProxyUseDefaultCredentials;
            }
            return proxy;
        }
        #endregion

        #region SetAccessToken
        public void SetAccessToken(string token)
        {
            AccessToken = token;
            IsInitialized = true;
        }
        #endregion

        #region OnlineCheck
        public async Task CheckOnlineAsync(int timeout = 10000)
        {
            if (IsConnecting) return; // Avoid multiple calls
            IsConnecting = true;
            bool isReachable = false;
            try
            {
                // Cancel after timeout
                CancellationTokenSource cts = new(new TimeSpan(0, 0, 0, 0, timeout));
                string uriString = $"{_appBaseUrl}";
                try
                {
                    if (httpClient is not null)
                    {
                        HttpResponseMessage? response = await httpClient.GetAsync(uriString, cts.Token).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        if (response != null)
                        {
                            isReachable = response.IsSuccessStatusCode;
                        }
                    }
                }
                catch (InvalidOperationException iexc)
                {
                    OnError(new UnhandledExceptionEventArgs(iexc, false));
                }
                catch (HttpRequestException rexc)
                {
                    OnError(new UnhandledExceptionEventArgs(rexc, false));
                }
                catch (TaskCanceledException)
                {
                    // Throws exception on timeout, not actually an error
                }
            }
            catch (Exception exc)
            {
                OnError(new UnhandledExceptionEventArgs(exc, false));
            }
            IsConnecting = false;
            IsOnline = isReachable;
            //return isReachable;
        }
        #endregion

        #region Contacts
        public async Task<List<LexContact>> GetContactsAsync(LexContactType type, int page = 0, int size = 25, int coolDown = 20)
        {
            List<LexContact> result = [];
            string cmd = string.Format("contacts{0}",
                type == LexContactType.Customer ? "?customer=true" : "?vendor=true"              
                );
            cmd += $"&page={page}&size={size}";

            string? jsonString = await BaseApiCallAsync(cmd, Method.Get) ?? string.Empty;
            LexContactsList? contacts = JsonConvert.DeserializeObject<LexContactsList>(jsonString);
            if (contacts != null)
            {
                result = new List<LexContact>(contacts.Content);
                if (page < contacts.TotalPages)
                {
                    page++;
                    List<LexContact> append = await GetContactsAsync(type, page, size);
                    result = new List<LexContact>(result.Concat(append));
                    await Task.Delay(coolDown < 20 ? 20 : coolDown);
                    return result;
                }
            }
            return result;
        }

        public async Task<LexContact?> GetContactAsync(Guid id)
        {
            string? jsonString = await BaseApiCallAsync($"contacts/{id}", Method.Get) ?? string.Empty;
            LexContact? contact = JsonConvert.DeserializeObject<LexContact>(jsonString);
            return contact;
        }
        #endregion

        #region Invoices
        public async Task<List<VoucherListContent>> GetInvoiceListAsync(LexVoucherStatus status, bool archived = false, int page = 0, int size = 25)
        {
            List<VoucherListContent> result = [];
            string type = LexVoucherType.invoice.ToString();
            string cmd = string.Format("voucherlist?{0}&{1}&{2}",
                $"voucherType={type}",
                $"voucherStatus={status}",
                $"archived={archived}"
                );
            cmd += $"&page={page}&size={size}";

            string? jsonString = await BaseApiCallAsync(cmd, Method.Get) ?? string.Empty;
            LexVoucherList? list = JsonConvert.DeserializeObject<LexVoucherList>(jsonString);
            if (list is not null)
            {
                if (page <= list.TotalPages)
                {
                    page++;
                    result = new List<VoucherListContent>(list.Content);
                    List<VoucherListContent> append = await GetInvoiceListAsync(status, archived, page, size);
                    result = new List<VoucherListContent>(result.Concat(append));
                    return result;
                }
            }
            return result;
        }

        public async Task<List<LexQuotation>> GetInvoicesAsync(List<Guid> ids)
        {
            List<LexQuotation> result = [];
            foreach (Guid id in ids)
            {
                LexQuotation? quote = await GetInvoiceAsync(id);
                if (quote is not null)
                    result.Add(quote);
            }
            return result;
        }

        public async Task<List<LexQuotation>> GetInvoicesAsync(List<VoucherListContent> voucherList)
        {
            List<Guid> ids = voucherList.Select(id => id.Id).ToList();
            return await GetInvoicesAsync(ids);
        }

        public async Task<LexQuotation?> GetInvoiceAsync(Guid id)
        {
            string? jsonString = await BaseApiCallAsync($"invoices/{id}", Method.Get) ?? string.Empty;
            LexQuotation? contact = JsonConvert.DeserializeObject<LexQuotation>(jsonString);
            return contact;
        }

        public async Task<LexInvoiceResponse?> AddInvoiceAsync(LexCreateInvoice lexQuotation, bool isFinalized = false)
        {
            string? jsonString = await BaseApiCallAsync($"invoices?finalize={isFinalized}", Method.Post, JsonConvert.SerializeObject(lexQuotation, jsonSerializerSettings)) ?? string.Empty;
            LexInvoiceResponse? contact = JsonConvert.DeserializeObject<LexInvoiceResponse>(jsonString);
            return contact;
        }
        #endregion

        #region Quotations
        public async Task<List<VoucherListContent>> GetQuotationListAsync(LexVoucherStatus status, bool archived = false, int page = 0, int size = 25)
        {
            List<VoucherListContent> result = [];
            string type = LexVoucherType.quotation.ToString();
            string cmd = string.Format("voucherlist?{0}&{1}&{2}",
                $"voucherType={type}",
                $"voucherStatus={status}",
                $"archived={archived}"
                );
            cmd += $"&page={page}&size={size}";

            string? jsonString = await BaseApiCallAsync(cmd, Method.Get) ?? string.Empty;
            LexVoucherList? list = JsonConvert.DeserializeObject<LexVoucherList>(jsonString);
            if (list is not null)
            {
                if (page < list.TotalPages - 1)
                {
                    page++;
                    result = new List<VoucherListContent>(list.Content);
                    List<VoucherListContent> append = await GetQuotationListAsync(status, archived, page, size);
                    result = new List<VoucherListContent>(result.Concat(append));
                    return result;
                }
            }
            return result;
        }

        public async Task<List<LexQuotation>> GetQuotationsAsync(List<Guid> ids)
        {
            List<LexQuotation> result = [];
            foreach (Guid Id in ids)
            {
                LexQuotation? quote = await GetQuotationAsync(Id);
                if (quote is not null)
                    result.Add(quote);
            }
            return result;
        }

        public async Task<List<LexQuotation>> GetQuotationsAsync(List<VoucherListContent> voucherList)
        {
            List<Guid> ids = voucherList.Select(id => id.Id).ToList();
            return await GetQuotationsAsync(ids);
        }

        public async Task<LexQuotation?> GetQuotationAsync(Guid id)
        {
            string? jsonString = await BaseApiCallAsync($"quotations/{id}", Method.Get) ?? string.Empty;
            LexQuotation? contact = JsonConvert.DeserializeObject<LexQuotation>(jsonString);
            return contact;
        }
        #endregion

        #endregion
    }
}
