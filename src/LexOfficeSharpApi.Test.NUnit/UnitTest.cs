using AndreasReitberger.API.LexOffice;
using AndreasReitberger.API.LexOffice.Enum;
using Newtonsoft.Json;

namespace LexOfficeSharpApi.Test.NUnit
{
    public class Tests
    {
        private readonly string tokenString = SecretAppSettingReader.ReadSection<SecretAppSetting>("TestSetup").ApiKey ?? "";
        private LexOfficeClient? client;

        #region Setup
        [SetUp]
        public void Setup()
        {
            client = new LexOfficeClient.LexOfficeConnectionBuilder()
                .WithApiKey(tokenString)
                .Build();
        }
        #endregion

        #region JSON
        [Test]
        public void TestJsonSerialization()
        {
            string? json = JsonConvert.SerializeObject(client, Formatting.Indented);
            Assert.That(!string.IsNullOrEmpty(json));

            var client2 = JsonConvert.DeserializeObject<LexOfficeClient>(json);
            Assert.That(client2 is not null);
        }
        #endregion

        #region Builder
        [Test]
        public async Task TestWithBuilder()
        {
            try
            {
                if (client is null) throw new NullReferenceException($"The client was null!");

                List<VoucherListContent> invoicesList = await client.GetInvoiceListAsync(LexVoucherStatus.Paid);
                List<LexDocumentResponse> invoices = await client.GetInvoicesAsync(invoicesList);

                Assert.That(invoices?.Count > 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region Invoices
        [Test]
        public async Task TestGetInvoicesOpen()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                List<VoucherListContent> invoicesList = await handler.GetInvoiceListAsync(LexVoucherStatus.Open);
                List<LexDocumentResponse> invoices = await handler.GetInvoicesAsync(invoicesList);

                Assert.That(invoices?.Count > 0);
            }
            catch (Exception ex) 
            {           
                Assert.Fail(ex.Message);
            }
        }

        [Test]
        public async Task TestCreateInvoices()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                // Create a new Invoice object
                LexDocumentResponse invoice = new()
                {
                    Address = new LexContactAddress()
                    {
                        Name = "Bike & Ride GmbH & Co. KG",
                        Supplement = "Gebäude 10",
                        Street = "Musterstraße 42",
                        City = "Freiburg",
                        Zip = "79112",
                        CountryCode = "DE",
                    },
                    LineItems =
                    [
                        new LexQuotationItem()
                        {
                            Type = "custom",
                            Name = "Energieriegel Testpaket",
                            Quantity = 1,
                            UnitName = "Stück",
                            UnitPrice = new LexQuotationUnitPrice()
                            {
                                Currency = "EUR",
                                NetAmount = 5,
                                TaxRatePercentage = 0
                            }
                        }
                    ],
                    TotalPrice = new LexQuotationTotalPrice()
                    {
                        Currency = "EUR",
                        TotalNetAmount = 10,
                        TotalGrossAmount = 10,
                        TotalTaxAmount = 10
                    },
                    TaxConditions = new LexQuotationTaxConditions()
                    {
                        TaxType = LexQuotationTaxType.Vatfree,
                    },
                    ShippingConditions = new LexShippingConditions()
                    {
                        ShippingDate = DateTime.Now,
                        ShippingEndDate = DateTime.Now,
                        ShippingType = "none"
                    },
                    PaymentConditions = new LexQuotationPaymentConditions()
                    {
                        PaymentTermLabel = "10 Tage - 3 %, 30 Tage netto",
                        PaymentTermLabelTemplate = "10 Tage - 3 %, 30 Tage netto",
                        PaymentTermDuration = 30,
                        PaymentDiscountConditions = new LexQuotationDiscountCondition() { DiscountPercentage = 3, DiscountRange = 10 }
                    },
                    VoucherDate = DateTime.Now,
                };
                LexResponseDefault? lexInvoiceResponse = await handler.AddInvoiceAsync(invoice, false);
                Assert.That(lexInvoiceResponse != null);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        [Test]
        public async Task TestGetInvoicesDraft()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                List<VoucherListContent> invoicesList = await handler.GetInvoiceListAsync(LexVoucherStatus.Draft);
                List<LexDocumentResponse> invoices = await handler.GetInvoicesAsync(invoicesList);

                Assert.That(invoices?.Count > 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        [Test]
        public async Task TestCreateCreditNoteDraft()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                List<VoucherListContent> invoicesList = await handler.GetInvoiceListAsync(LexVoucherStatus.Open);

                List<LexDocumentResponse> invoices = await handler.GetInvoicesAsync(invoicesList);
                var invoice = invoices.FirstOrDefault();

                var voucherNumber = invoice.VoucherNumber;

                invoice.Title = "Rechnungskorrektur";
                invoice.Introduction = $"Rechnungskorrektur zur Rechnung {voucherNumber}";

                invoice.LineItems.Add(
                    new LexQuotationItem()
                    {
                        Type = "custom",
                        Name = "Energieriegel Testpaket",
                        Quantity = 0.1m,
                        UnitName = "Stück",
                        UnitPrice = new LexQuotationUnitPrice()
                        {
                            Currency = "EUR",
                            NetAmount = -150,
                            TaxRatePercentage = 0
                        }
                    }
                );

                var rs = await handler.AddCreditNoteAsync(invoice, true);

                Assert.That(rs != null);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        [Test]
        public async Task TestAddEventSubscription()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                // Cleanup available event subscriptions
                List<LexResponseDefault> allSubscriptions = await handler.GetAllEventSubscriptionsAsync();
                foreach(var subscriptions in allSubscriptions)
                {
                    await handler.DeleteEventSubscriptionAsync(subscriptions.SubscriptionId);
                }

                // Create event subscription
                LexResponseDefault newSubscription = await handler.AddEventSubscriptionAsync(new LexResponseDefault
                {
                    EventType = EventTypes.PaymentChanged,
                    CallbackUrl = "https://webhook.site/11dac08c-7a64-4467-aae9-8ec5dd1f3338"
                });

                LexResponseDefault subscription = await handler.GetEventSubscriptionAsync(newSubscription.Id);

                Assert.That(newSubscription != null);
                Assert.That(newSubscription.Id != subscription.Id);
                Assert.That(newSubscription.EventType != subscription.EventType);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region Payments
        [Test]
        public async Task TestGetPayments()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                List<VoucherListContent> availableInvoices = await handler.GetInvoiceListAsync(LexVoucherStatus.Paid, size: 1, pages: 1);
                Guid invoiceId = availableInvoices.First().Id; // Guid.Parse("YOUR_INVOICE_ID");
                Assert.That(invoiceId != Guid.Empty);

                LexPayments? payments = await handler.GetPaymentsAsync(invoiceId);

                Assert.That(payments != null);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
      
        #endregion
          
        #region Files

        [Test]
        public async Task TestRenderDocumentAsync()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                List<VoucherListContent> availableInvoices = await handler.GetInvoiceListAsync(LexVoucherStatus.Paid, size: 1, pages: 1);
                Guid invoiceId = availableInvoices.First().Id; // Guid.Parse("YOUR_INVOICE_ID");
                Assert.That(invoiceId != Guid.Empty);

                LexDocumentResponse? invoice = await handler.GetInvoiceAsync(availableInvoices.First().Id);

                LexQuotationFiles? files = await handler.RenderDocumentAsync(invoiceId);

                Assert.That(files != null);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        [Test]
        public async Task TestGetFileAsync()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);

                List<VoucherListContent> availableInvoices = await handler.GetInvoiceListAsync(LexVoucherStatus.Paid, size: 1, pages: 1);
                Guid invoiceId = availableInvoices.First().Id; // Guid.Parse("YOUR_INVOICE_ID");
                Assert.That(invoiceId != Guid.Empty);

                LexDocumentResponse? invoice = await handler.GetInvoiceAsync(availableInvoices.First().Id);
                LexQuotationFiles? files = await handler.RenderDocumentAsync(invoiceId);
                Assert.That(files is not null);

                Guid documentId = files.DocumentFileId;// Guid.Parse("YOUR_FILE_ID");
                byte[] file = await handler.GetFileAsync(documentId);

                Assert.That(file is not null);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

       
        #endregion

        #region Countries
        [Test]
        public async Task TestGetCountries()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);
                List<LexCountry> list = await handler.GetCountriesAsync();
                Assert.That(list?.Count > 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region Credit Notes
        [Test]
        public async Task TestGetCreditNotes()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);
                List<LexDocumentResponse> list = await handler.GetCreditNotesAsync();
                Assert.That(list?.Count > 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region PaymentConditions
        [Test]
        public async Task TestGetPaymentConditions()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);
                List<LexQuotationPaymentConditions> paymentConditions = await handler.GetPaymentConditionsAsync();
                Assert.That(paymentConditions?.Count > 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region Quotations
        [Test]
        public async Task TestGetQuotations()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);
                List<VoucherListContent> listContent = await handler.GetQuotationListAsync(LexVoucherStatus.Accepted);
                List<LexDocumentResponse> list = await handler.GetQuotationsAsync(listContent);
                Assert.That(list?.Count > 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region Contacts
        [Test]
        public async Task TestGetContacts()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);
                List<LexContact> list = await handler.GetContactsAsync(LexContactType.Customer, size: 100, pages: 2);
                Assert.That(list?.Count > 0);

                await Task.Delay(500);

                list = await handler.GetContactsAsync(LexContactType.Vendor, size: 100, pages: 2);
                Assert.That(list?.Count > 0);

                Guid id = list.FirstOrDefault().Id;
                var contact = await handler.GetContactAsync(id);
                Assert.That(contact is not null);

            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
        #endregion

        #region RateLimiter
        [Test]
        public async Task TestRateLimiterOnGetContacts()
        {
            try
            {
                LexOfficeClient handler = new(tokenString);
                List<LexContact> list = await handler.GetContactsAsync(LexContactType.Customer, size: 100, pages: -1);
                Assert.That(list?.Count > 0);

                await Task.Delay(500);

                list = await handler.GetContactsAsync(LexContactType.Vendor, size: 100, pages: 2);
                Assert.That(list?.Count > 0);

                Guid id = list.FirstOrDefault().Id;
                var contact = await handler.GetContactAsync(id);
                Assert.That(contact is not null);

            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        #endregion
    }
}