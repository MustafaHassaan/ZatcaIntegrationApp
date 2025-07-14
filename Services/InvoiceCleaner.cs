using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ZatcaIntegrationApp.Services
{
    public static class InvoiceCleaner
    {
        public static XmlDocument CleanInvoice(string filePath)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(filePath);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
            nsmgr.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            nsmgr.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
            nsmgr.AddNamespace("inv", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");

            // 🗑️ Remove <ext:UBLExtensions>
            var extensionsNode = doc.SelectSingleNode("//ext:UBLExtensions", nsmgr);
            extensionsNode?.ParentNode?.RemoveChild(extensionsNode);

            // 🗑️ Remove <cac:Signature>
            var signatureNode = doc.SelectSingleNode("//cac:Signature", nsmgr);
            signatureNode?.ParentNode?.RemoveChild(signatureNode);

            // 🗑️ Remove <cac:AdditionalDocumentReference> where <cbc:ID> is QR or PIH or ICV
            var docRefNodes = doc.SelectNodes("//cac:AdditionalDocumentReference", nsmgr);
            foreach (XmlNode node in docRefNodes)
            {
                var idNode = node.SelectSingleNode("cbc:ID", nsmgr);
                if (idNode != null && (idNode.InnerText == "QR" || idNode.InnerText == "PIH" || idNode.InnerText == "ICV"))
                {
                    node.ParentNode?.RemoveChild(node);
                }
            }

            return doc;
        }
        public static void SaveCleanInvoice(XmlDocument doc, string outputPath)
        {
            doc.Save(outputPath);
        }
    }
}