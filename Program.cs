using net.sf.saxon.functions;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml;
using ZatcaIntegrationApp.Helpers;
using ZatcaIntegrationApp.Services;

string CertificatePath = "Data/Certificates/certificate.csr";
string PrivateKeyPath = "Data/Certificates/private.key";
string PihPath = "Data/Certificates/pih.txt";
string InputPath = "Data/Invoice.xml";
string SignedOutputPath = "Data/signed_invoice.xml";
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("===============================================");
Console.WriteLine("     ZATCA E-Invoice Integration - Sandbox");
Console.WriteLine("===============================================\n");
var invoiceService = new InvoiceService();
// ============ 1. توليد CSR وPrivateKey ============
//1-انشاء ملفات الشركه
invoiceService.GenerateCSR();

// ============ 2. رفع الـ CSR ======================
//2- طلب تسجيل بيانات الشركه
await invoiceService.UploadCsrAsync();

// ============ 3. Get CER ======================
//2- طلب الحصول على شهادة الشركة
await invoiceService.Getcertificate();

// ============ 4. Make UBL ======================
//4-عمل فاتوره
invoiceService.CreateSampleInvoiceFromCsrData();

// ============ 5. Sign UBL ======================
//5-تحميل الفاتورة المنظّفة
var xmlContent = File.ReadAllText(InputPath);
var invoiceDoc = invoiceService.LoadInvoiceFromString(xmlContent);
await invoiceService.Sign(invoiceDoc);

Logger.LogSuccess("🎉 The process was completed successfully .");
Console.ReadKey();
//===================================================
