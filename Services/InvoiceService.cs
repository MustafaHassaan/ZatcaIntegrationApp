using net.sf.saxon.functions;
using Newtonsoft.Json;
using org.apache.xerces.xni;
using Org.BouncyCastle.Crypto.Macs;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Zatca.EInvoice.SDK;
using Zatca.EInvoice.SDK.Contracts.Models;
using ZatcaIntegrationApp.Helpers;
using ZatcaIntegrationApp.Models;
using static net.sf.saxon.om.AbsolutePath;

namespace ZatcaIntegrationApp.Services
{
    public class InvoiceService
    {
        private readonly EInvoiceValidator _validator;
        private readonly EInvoiceSigner _signer;
        private readonly RequestGenerator _requestGenerator;
        public CsrGenerationDto _CSRDTO;
        private readonly EInvoiceHashGenerator _IHG;
        private readonly EInvoiceQRGenerator _QRdata;
        Complianceinvoice CI = new Complianceinvoice();
        public InvoiceService()
        {
            _validator = new EInvoiceValidator();
            _signer = new EInvoiceSigner();
            _requestGenerator = new RequestGenerator();
            _IHG = new EInvoiceHashGenerator();
            _QRdata = new EInvoiceQRGenerator();
        }
        public void GenerateCSR()
        {
            Console.WriteLine("===== Please Press Any Key To Generate Data Company Certificate . ====\n");
            Console.ReadKey();
            var generator = new CsrGenerator();
            _CSRDTO = new CsrGenerationDto(
                commonName: "TST-886431145-399999999900003",
                serialNumber: "1-Boomsandwitch|2-Easybos|3-"+ Guid.NewGuid().ToString(),
                organizationIdentifier: "399999999900003",
                organizationUnitName: "3999999999",
                organizationName: "Boomsandwitch",
                countryName: "SA",
                invoiceType: "1100",
                locationAddress: "Heddah",
                industryBusinessCategory: "Foodes"
            );
            var result = generator.GenerateCsr(_CSRDTO, EnvironmentType.NonProduction, pemFormat: true);
            if (!result.IsValid)
            {
                Console.WriteLine("❌ Fail Generate Data Company Certificate");
                foreach (var error in result.ErrorMessages)
                    Console.WriteLine("- " + error);
                Console.ReadKey();
            }
            string csrBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(result.Csr));
            string keyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(result.PrivateKey));
            File.WriteAllText("Data/Certificates/certificate.csr", csrBase64);
            File.WriteAllText("Data/Certificates/private.key", keyBase64);
            Logger.LogSuccess("✅ Done Generate Data Company Certificate And PrivateKey \n");
            Console.WriteLine("===== Please Press Any Key To Request Company data registration. ====\n");
            Console.ReadKey();
        }
        public XmlDocument LoadInvoiceFromString(string xmlString)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlString);
            return doc;
        }
        public async Task Sign(XmlDocument doc)
        {
            Console.WriteLine("===== Please Press Any Key To Sign UBL XML . ====\n");
            Console.ReadKey();
            // Json File Read
            string Certjson = File.ReadAllText("Data/Certificates/CSR.Json");
            var certData = JsonConvert.DeserializeObject<CSR>(Certjson);
            // شهادة
            string base64Cert = certData.binarySecurityToken;
            byte[] certBytes = Convert.FromBase64String(base64Cert);
            string certPem = Encoding.UTF8.GetString(certBytes);

            // مفتاح خاص
            string base64Key = File.ReadAllText("Data/Certificates/private.key");
            byte[] keyBytes = Convert.FromBase64String(base64Key);
            string keyPem = Encoding.UTF8.GetString(keyBytes);

            // تحميل الشهادة بالطريقة الصحيحة
            var certdata = new X509Certificate2(Encoding.UTF8.GetBytes(certPem));
            var issuer = certdata.Issuer;
            var serial = certdata.SerialNumber;
            Console.WriteLine(certdata + "\n" + issuer + "\n" + serial);
            var result = _signer.SignDocument(doc, certPem, keyPem);
            if (!result.IsValid)
            {
                Console.WriteLine("❌ Signing Failed");
                foreach (var error in result.Steps)
                    Console.WriteLine("- " + error.IsValid.ToString() + ":" + error.StepName + " : " + error.ErrorMessages);
                Console.ReadKey();
            }
            else
            {
                Console.WriteLine("✅ Signed invoice is done scuccess ... ");
                foreach (var error in result.Steps)
                {
                    Console.WriteLine($"- {error.IsValid} : {error.StepName}");

                    if (error.ErrorMessages != null && error.ErrorMessages.Any())
                    {
                        foreach (var msg in error.ErrorMessages)
                        {
                            Console.WriteLine($"    🔴 {msg}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("    ✅ No error messages.");
                    }
                }
                //foreach (var error in result.Steps)
                //    Console.WriteLine("- " + error.IsValid.ToString() + ":" + error.StepName + " : " + error.ErrorMessages);
                //Console.ReadKey();
                File.WriteAllText("Data/Signedinvoice.xml", result.SignedEInvoice.OuterXml);
                Logger.LogSuccess("✅ Signed invoice saved to: " + "Data/Signedinvoice.xml");
                Console.WriteLine("===== Please Press Any Key To Send Zatca Invoice . ====\n");
                Console.ReadKey();
                var xmlContent = File.ReadAllText("Data/Signedinvoice.xml");
                var invoiceDoc = LoadInvoiceFromString(xmlContent);
                await GenerateRequest(invoiceDoc);
            }
        }
        public async Task SendInvoiceAsync(string invoiceHash, string uuid, string base64Invoice)
        {
            using var client = new HttpClient();
            var payload = new
            {
                invoiceHash = invoiceHash,
                uuid = uuid,
                invoice = base64Invoice
            };
            // Json File Read
            string Certjson = File.ReadAllText("Data/Certificates/CSR.Json");
            var certData = JsonConvert.DeserializeObject<CSR>(Certjson);
            // إعداد الرؤوس
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("accept-language", "ar");
            client.DefaultRequestHeaders.Add("Clearance-Status", "0");
            client.DefaultRequestHeaders.Add("Accept-Version", "V2");
            string rawCredentials = $"{certData.binarySecurityToken}:{certData.secret}";
            string base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/invoices/reporting/single";

            var response = await client.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();
            var zatcaResponse = JsonConvert.DeserializeObject<ZATCAResponse>(responseText);
            Logger.LogSuccess(response.ReasonPhrase);
            if (zatcaResponse.reportingStatus == "REPORTED")
            {
                Logger.LogSuccess(zatcaResponse.reportingStatus);
            }
            else
            {
                Logger.LogError(zatcaResponse.reportingStatus);
            }
            Console.WriteLine("=================== The End =====================\n");
        }
        public async Task GenerateRequest(XmlDocument signedDoc)
        {
            // 1. توليد نتيجة الطلب من SDK
            var result = _requestGenerator.GenerateRequest(signedDoc);

            // 2. إعداد الـ NamespaceManager
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(signedDoc.NameTable);
            nsmgr.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            nsmgr.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

            // 3. تعديل أو إدراج عنصر PIH بين ICV و QR
            var pihNode = signedDoc.SelectSingleNode("//cbc:EmbeddedDocumentBinaryObject[../cbc:ID='PIH']", nsmgr);

            if (pihNode != null)
            {
                pihNode.InnerText = result.InvoiceRequest.InvoiceHash;
                Logger.LogInfo("🔁 تم تعديل قيمة PIH داخل العنصر الموجود");
            }
            else
            {
                XmlElement pihRef = signedDoc.CreateElement("cac", "AdditionalDocumentReference", nsmgr.LookupNamespace("cac"));
                XmlElement pihId = signedDoc.CreateElement("cbc", "ID", nsmgr.LookupNamespace("cbc"));
                pihId.InnerText = "PIH";

                XmlElement attachment = signedDoc.CreateElement("cac", "Attachment", nsmgr.LookupNamespace("cac"));
                XmlElement embeddedObj = signedDoc.CreateElement("cbc", "EmbeddedDocumentBinaryObject", nsmgr.LookupNamespace("cbc"));
                embeddedObj.SetAttribute("mimeCode", "text/plain");
                embeddedObj.InnerText = result.InvoiceRequest.InvoiceHash;

                attachment.AppendChild(embeddedObj);
                pihRef.AppendChild(pihId);
                pihRef.AppendChild(attachment);

                // إدراج بعد عنصر ICV
                var icvNode = signedDoc.SelectSingleNode("//cac:AdditionalDocumentReference[cbc:ID='ICV']", nsmgr);
                signedDoc.DocumentElement.InsertAfter(pihRef, icvNode);
            }

            // 4. حفظ النسخة المعدلة
            string path = "Data/Signedinvoice.xml";
            signedDoc.Save(path);
            //Logger.LogInfo("✅ The amended invoice is saved in : " + path);
            string Oldpath = @"Data/Invoice.xml";
            if (File.Exists(Oldpath))
            {
                File.Delete(Oldpath);
            }

            var xmlContent = File.ReadAllText("Data/Signedinvoice.xml");
            var invoiceDoc = LoadInvoiceFromString(xmlContent);

            // Json File Read
            string Certjson = File.ReadAllText("Data/Certificates/CSR.Json");
            var certData = JsonConvert.DeserializeObject<CSR>(Certjson);

            // خطوة 1: تحقق داخلي
            var validation = _validator.ValidateEInvoice(invoiceDoc, certData.binarySecurityToken, result.InvoiceRequest.InvoiceHash);
            if (!validation.IsValid)
            {
                //Console.WriteLine("❌ Fail The Invoice Is not compliant ... ");
                //foreach (var error in validation.ValidationSteps)
                //{
                //    Console.WriteLine($"- {error.IsValid} : {error.ValidationStepName}");

                //    if (error.ErrorMessages != null && error.ErrorMessages.Any())
                //    {
                //        foreach (var msg in error.ErrorMessages)
                //        {
                //            Console.WriteLine($"    ❗ {msg}");
                //        }
                //    }
                //    else
                //    {
                //        Console.WriteLine("    ✅ No error messages.");
                //    }
                //}
                //foreach (var error in validation.ValidationSteps)
                //    Console.WriteLine("- " + error.IsValid.ToString() + ":" + error.ValidationStepName + " : " + error.ErrorMessages);
                //Console.ReadKey();
                //Logger.LogWarning("❌ Non-incriminating error ... ");
                //return;

                //Logger.LogInfo("Invoice Hash (PIH): " + result.InvoiceRequest.InvoiceHash);
                //Logger.LogInfo("Invoice UUID: " + result.InvoiceRequest.Uuid);
                //Logger.LogInfo("Invoice : " + result.InvoiceRequest.Invoice);

                //Console.ReadKey();

                await SendInvoiceAsync(result.InvoiceRequest.InvoiceHash,
                                 result.InvoiceRequest.Uuid,
                                 result.InvoiceRequest.Invoice);
            }
        }
        public void CreateSampleInvoiceFromCsrData()
        {
            var products = new List<ProductLine>
            {
                new ProductLine { Id = "1", Name = "قلم رصاص", Quantity = 2, UnitPrice = 2 },
                new ProductLine { Id = "2", Name = "دفتر", Quantity = 1, UnitPrice = 5 },
                new ProductLine { Id = "3", Name = "ممحاة", Quantity = 3, UnitPrice = 1.5m },
                new ProductLine { Id = "4", Name = "آلة حاسبة", Quantity = 1, UnitPrice = 30 },
                new ProductLine { Id = "5", Name = "مسطرة", Quantity = 2, UnitPrice = 3 }
            };
            decimal totalExtension = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;
            decimal total = 0;
            decimal totalPrice = 0;
            decimal totalAmount = 0;

            foreach (var p in products)
            {
                decimal netPrice = p.UnitPrice - p.Discount;
                decimal lineTotal = netPrice * p.Quantity;
                totalPrice += lineTotal;
                decimal taxAmount = Math.Round(lineTotal * p.TaxPercent / 100, 2);

                totalExtension += lineTotal;
                totalTax += taxAmount;
                totalDiscount += p.Discount * p.Quantity;
                total = totalTax + lineTotal;
                Console.WriteLine($"Product: {p.Name}, Quantity: {p.Quantity}, Unit Price: {p.UnitPrice}, Line Total: {lineTotal}, Tax Amount: {taxAmount}, Total Amount: {taxAmount + lineTotal}");
            }
            Console.WriteLine($"Price Amount : {totalPrice}");
            Console.WriteLine($"Tax Amount : {totalTax}");
            totalAmount = totalPrice + totalTax;
            Console.WriteLine($"Total : {totalPrice + totalTax}");
            //Console.ReadKey();
            if (_CSRDTO == null)
            {
                throw new InvalidOperationException("❌ CSR Data is missing. تأكد من استدعاء GenerateCSR() أولاً.");
            }
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) // 🔥 UTF-8 بدون BOM
            };

            using (var fs = new FileStream("Data/Invoice.xml", FileMode.Create, FileAccess.Write))
            using (var writer = XmlWriter.Create(fs, settings))
            {
                writer.WriteStartDocument();

                // Root element
                writer.WriteStartElement("Invoice", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");

                // Namespaces
                writer.WriteAttributeString("xmlns", "cac", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
                writer.WriteAttributeString("xmlns", "cbc", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
                writer.WriteAttributeString("xmlns", "ext", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");

                // UBLExtensions - جاهز لاستقبال التوقيع لاحقًا
                writer.WriteStartElement("ext", "UBLExtensions", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
                writer.WriteStartElement("ext", "UBLExtension", null);
                writer.WriteStartElement("ext", "ExtensionContent", null);
                // التوقيع هينضاف هنا لاحقًا من خلال SDK
                writer.WriteEndElement(); // ExtensionContent
                writer.WriteEndElement(); // UBLExtension
                writer.WriteEndElement(); // UBLExtensions

                // Header
                writer.WriteElementString("cbc", "ProfileID", null, "reporting:1.0");
                writer.WriteElementString("cbc", "ID", null, "INV-001");
                writer.WriteElementString("cbc", "UUID", null, Guid.NewGuid().ToString());
                writer.WriteElementString("cbc", "IssueDate", null, DateTime.Now.ToString("yyyy-MM-dd"));
                writer.WriteElementString("cbc", "IssueTime", null, DateTime.Now.ToString("HH:mm:ss"));

                // Invoice Type Code
                writer.WriteStartElement("cbc", "InvoiceTypeCode", null);
                writer.WriteAttributeString("name", "0100000");
                writer.WriteString("388");
                writer.WriteEndElement();

                writer.WriteElementString("cbc", "DocumentCurrencyCode", null, "SAR");
                writer.WriteElementString("cbc", "TaxCurrencyCode", null, "SAR");

                // ICV
                writer.WriteStartElement("cac", "AdditionalDocumentReference", null);

                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteString("ICV");
                writer.WriteEndElement(); // 👈 نهاية العنصر ID

                writer.WriteStartElement("cbc", "UUID", null);
                writer.WriteString("10");
                writer.WriteEndElement(); // 👈 نهاية العنصر UUID

                writer.WriteEndElement(); // 👈 نهاية العنصر AdditionalDocumentReference

                // Supplier - AccountingSupplierParty
                writer.WriteStartElement("cac", "AccountingSupplierParty", null);
                writer.WriteStartElement("cac", "Party", null);

                // PartyIdentification
                writer.WriteStartElement("cac", "PartyIdentification", null);
                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteAttributeString("schemeID", "CRN");
                writer.WriteString("1010010000");
                writer.WriteEndElement(); // cbc:ID
                writer.WriteEndElement(); // PartyIdentification

                // PostalAddress
                writer.WriteStartElement("cac", "PostalAddress", null);
                writer.WriteElementString("cbc", "StreetName", null, "الامير سلطان");
                writer.WriteElementString("cbc", "BuildingNumber", null, "2322");
                writer.WriteElementString("cbc", "CitySubdivisionName", null, "المربع");
                writer.WriteElementString("cbc", "CityName", null, "الرياض");
                writer.WriteElementString("cbc", "PostalZone", null, "23333");
                writer.WriteStartElement("cac", "Country", null);
                writer.WriteElementString("cbc", "IdentificationCode", null, "SA");
                writer.WriteEndElement(); // Country
                writer.WriteEndElement(); // PostalAddress

                // PartyTaxScheme
                writer.WriteStartElement("cac", "PartyTaxScheme", null);
                writer.WriteElementString("cbc", "CompanyID", null, _CSRDTO.OrganizationIdentifier);
                writer.WriteStartElement("cac", "TaxScheme", null);
                writer.WriteElementString("cbc", "ID", null, "VAT");
                writer.WriteEndElement(); // TaxScheme
                writer.WriteEndElement(); // PartyTaxScheme

                // PartyLegalEntity
                writer.WriteStartElement("cac", "PartyLegalEntity", null);
                writer.WriteElementString("cbc", "RegistrationName", null, _CSRDTO.OrganizationName);
                writer.WriteEndElement(); // PartyLegalEntity

                writer.WriteEndElement(); // Party
                writer.WriteEndElement(); // AccountingSupplierParty

                // Customer - AccountingCustomerParty
                
                writer.WriteStartElement("cac", "AccountingCustomerParty", null);
                writer.WriteStartElement("cac", "Party", null);

                // PartyIdentification
                writer.WriteStartElement("cac", "PartyIdentification", null);
                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteAttributeString("schemeID", "CRN");
                writer.WriteString("1010010000");
                writer.WriteEndElement(); // cbc:ID
                writer.WriteEndElement(); // PartyIdentification

                // PostalAddress
                writer.WriteStartElement("cac", "PostalAddress", null);
                writer.WriteElementString("cbc", "StreetName", null, "الامير سلطان");
                writer.WriteElementString("cbc", "BuildingNumber", null, "2322");
                writer.WriteElementString("cbc", "CitySubdivisionName", null, "المربع");
                writer.WriteElementString("cbc", "CityName", null, "الرياض");
                writer.WriteElementString("cbc", "PostalZone", null, "23333");
                writer.WriteStartElement("cac", "Country", null);
                writer.WriteElementString("cbc", "IdentificationCode", null, "SA");
                writer.WriteEndElement(); // Country
                writer.WriteEndElement(); // PostalAddress

                // PartyTaxScheme
                writer.WriteStartElement("cac", "PartyTaxScheme", null);
                writer.WriteElementString("cbc", "CompanyID", null, "399999999800003");
                writer.WriteStartElement("cac", "TaxScheme", null);
                writer.WriteElementString("cbc", "ID", null, "VAT");
                writer.WriteEndElement(); // TaxScheme
                writer.WriteEndElement(); // PartyTaxScheme

                // PartyLegalEntity
                writer.WriteStartElement("cac", "PartyLegalEntity", null);
                writer.WriteElementString("cbc", "RegistrationName", null, _CSRDTO.OrganizationName);
                writer.WriteEndElement(); // PartyLegalEntity

                writer.WriteEndElement(); // Party
                writer.WriteEndElement(); // AccountingCustomerParty

                // Delivery
                writer.WriteStartElement("cac", "Delivery", null);
                writer.WriteElementString("cbc", "ActualDeliveryDate", null, DateTime.Now.ToString("yyyy-MM-dd"));
                writer.WriteEndElement();

                //PaymentMeans
                writer.WriteStartElement("cac", "PaymentMeans", null);
                writer.WriteElementString("cbc", "PaymentMeansCode", null, "10");
                writer.WriteEndElement();

                writer.WriteStartElement("cac", "AllowanceCharge", null);

                writer.WriteElementString("cbc", "ChargeIndicator", null, "false");
                writer.WriteElementString("cbc", "AllowanceChargeReason", null, "discount");

                // Amount with currency attribute
                writer.WriteStartElement("cbc", "Amount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString("0.00");
                writer.WriteEndElement(); // Amount

                // TaxCategory
                writer.WriteStartElement("cac", "TaxCategory", null);

                // ID with attributes
                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteAttributeString("schemeID", "UN/ECE 5305");
                writer.WriteAttributeString("schemeAgencyID", "6");
                writer.WriteString("S");
                writer.WriteEndElement(); // ID

                writer.WriteElementString("cbc", "Percent", null, "15");

                // TaxScheme
                writer.WriteStartElement("cac", "TaxScheme", null);
                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteAttributeString("schemeID", "UN/ECE 5153");
                writer.WriteAttributeString("schemeAgencyID", "6");
                writer.WriteString("VAT");
                writer.WriteEndElement(); // ID inside TaxScheme
                writer.WriteEndElement(); // TaxScheme

                writer.WriteEndElement(); // TaxCategory
                writer.WriteEndElement(); // AllowanceCharge


                // Tax Total
                writer.WriteStartElement("cac", "TaxTotal", null);
                writer.WriteStartElement("cbc", "TaxAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalTax.ToString());
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("cac", "TaxTotal", null);
                writer.WriteStartElement("cbc", "TaxAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalTax.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("cac", "TaxSubtotal", null);
                writer.WriteStartElement("cbc", "TaxableAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalPrice.ToString());
                writer.WriteEndElement();
                writer.WriteStartElement("cbc", "TaxAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalTax.ToString());
                writer.WriteEndElement();
                writer.WriteStartElement("cac", "TaxCategory", null);
                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteAttributeString("schemeID", "UN/ECE 5305");
                writer.WriteAttributeString("schemeAgencyID", "6");
                writer.WriteString("S");
                writer.WriteEndElement();

                writer.WriteElementString("cbc", "Percent", null, "15.00");
                writer.WriteStartElement("cac", "TaxScheme", null);
                writer.WriteStartElement("cbc", "ID", null);
                writer.WriteAttributeString("schemeID", "UN/ECE 5153");
                writer.WriteAttributeString("schemeAgencyID", "6");
                writer.WriteString("VAT");
                writer.WriteEndElement();

                writer.WriteEndElement(); // TaxScheme
                writer.WriteEndElement(); // TaxCategory
                writer.WriteEndElement(); // TaxSubtotal
                writer.WriteEndElement(); // TaxTotal

                // Legal Monetary Total
                writer.WriteStartElement("cac", "LegalMonetaryTotal", null);

                writer.WriteStartElement("cbc", "LineExtensionAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalPrice.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("cbc", "TaxExclusiveAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalPrice.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("cbc", "TaxInclusiveAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalAmount.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("cbc", "AllowanceTotalAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString("0.00");
                writer.WriteEndElement();

                writer.WriteStartElement("cbc", "PrepaidAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString("0.00");
                writer.WriteEndElement();

                writer.WriteStartElement("cbc", "PayableAmount", null);
                writer.WriteAttributeString("currencyID", "SAR");
                writer.WriteString(totalAmount.ToString());
                writer.WriteEndElement();

                writer.WriteEndElement(); // LegalMonetaryTotal
                foreach (var p in products)
                {
                    decimal netPrice = p.UnitPrice - p.Discount;
                    decimal lineTotal = netPrice * p.Quantity;
                    decimal taxAmount = Math.Round(lineTotal * p.TaxPercent / 100, 2);
                    // Invoice Line
                    writer.WriteStartElement("cac", "InvoiceLine", null);

                    writer.WriteElementString("cbc", "ID", null, p.Id);

                    writer.WriteStartElement("cbc", "InvoicedQuantity", null);
                    writer.WriteAttributeString("unitCode", "PCE");
                    writer.WriteString(p.Quantity.ToString());
                    writer.WriteEndElement();

                    writer.WriteStartElement("cbc", "LineExtensionAmount", null);
                    writer.WriteAttributeString("currencyID", "SAR");
                    writer.WriteString(p.UnitPrice.ToString());
                    writer.WriteEndElement();

                    // TaxTotal
                    writer.WriteStartElement("cac", "TaxTotal", null);
                    writer.WriteStartElement("cbc", "TaxAmount", null);
                    writer.WriteAttributeString("currencyID", "SAR");
                    writer.WriteString(taxAmount.ToString());
                    writer.WriteEndElement();

                    writer.WriteStartElement("cbc", "RoundingAmount", null);
                    writer.WriteAttributeString("currencyID", "SAR");
                    writer.WriteString((taxAmount + lineTotal).ToString());
                    writer.WriteEndElement();
                    writer.WriteEndElement(); // TaxTotal

                    // Item
                    writer.WriteStartElement("cac", "Item", null);
                    writer.WriteElementString("cbc", "Name", null, p.Name);

                    // ClassifiedTaxCategory
                    writer.WriteStartElement("cac", "ClassifiedTaxCategory", null);
                    writer.WriteElementString("cbc", "ID", null, "S");
                    writer.WriteElementString("cbc", "Percent", null, "15.00");

                    // TaxScheme
                    writer.WriteStartElement("cac", "TaxScheme", null);
                    writer.WriteElementString("cbc", "ID", null, "VAT");
                    writer.WriteEndElement(); // TaxScheme

                    writer.WriteEndElement(); // ClassifiedTaxCategory
                    writer.WriteEndElement(); // Item

                    // Price
                    writer.WriteStartElement("cac", "Price", null);
                    writer.WriteStartElement("cbc", "PriceAmount", null);
                    writer.WriteAttributeString("currencyID", "SAR");
                    writer.WriteString(p.UnitPrice.ToString());
                    writer.WriteEndElement();

                    // AllowanceCharge inside Price
                    writer.WriteStartElement("cac", "AllowanceCharge", null);
                    writer.WriteElementString("cbc", "ChargeIndicator", null, "false");
                    writer.WriteElementString("cbc", "AllowanceChargeReason", null, "discount");

                    writer.WriteStartElement("cbc", "Amount", null);
                    writer.WriteAttributeString("currencyID", "SAR");
                    writer.WriteString("0.00");
                    writer.WriteEndElement(); // Amount

                    writer.WriteEndElement(); // AllowanceCharge
                    writer.WriteEndElement(); // Price

                    writer.WriteEndElement(); // InvoiceLine
                }
                writer.WriteEndElement(); // Invoice
                writer.WriteEndDocument();

            }
            Logger.LogSuccess("✅ The invoice has been created .\n");
            //Console.ReadKey();
            Logger.LogSuccess("✅ Invoice XML saved to: " + "Data/Invoice.xml\n");
            //Console.ReadKey();
            //return sb.ToString();
        }
        public async Task UploadCsrAsync()
        {
            var data = File.ReadAllText("Data/Certificates/certificate.csr");
            var payload = new { csr = data };
            var json = JsonConvert.SerializeObject(payload);

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Accept-Version", "V2");
            http.DefaultRequestHeaders.Add("OTP", "123345");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var res = await http.PostAsync(
                "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/compliance",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("❌ Fail Upload Data Company \n" + body);
                return;
            }

            Logger.LogSuccess("✅ Done Upload Data Company\n");
            //Console.WriteLine(body +"\n");
            var obj = JsonConvert.DeserializeObject(body);
            File.WriteAllText("Data/Certificates/CSR.Json", body);
            Console.WriteLine("===== Please Press Any Key To Request Token. ====\n");
            Console.ReadKey();
        }
        public async Task Getcertificate()
        {
            CSR csrt= new CSR();
            var data = File.ReadAllText("Data/Certificates/CSR.Json");
            var csrtdata = JsonConvert.DeserializeObject<CSR>(data);
            var payload = new { compliance_request_id = csrtdata.requestID };
            var json = JsonConvert.SerializeObject(payload);
            string credentials = $"{csrtdata.binarySecurityToken}:{csrtdata.secret}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Accept-Version", "V2");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var res = await http.PostAsync(
                "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/production/csids",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                Logger.LogError("❌ Fail Generated Certification \n" + body);
                return;
            }
            Logger.LogSuccess("✅ Done Generated Token\n");
            var csrtbody = JsonConvert.DeserializeObject<CSR>(body);
            Console.WriteLine("===== Please Press Any Key To Save Token . ====\n");
            Console.ReadKey();
            File.WriteAllText("Data/Certificates/CSR.Json", body);
            Logger.LogSuccess("✅ Done Save Token\n");
            Console.WriteLine("===== Make Data For Test . ====\n");
            Console.ReadKey();
        }
    }
}
