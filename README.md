# ZATCA Integration App (.NET Core 8)

An advanced integration app built on .NET Core 8 that facilitates full interaction with the e-invoicing ecosystem of the Zakat, Tax, and Customs Authority (ZATCA), utilizing the official SDK to report simplified invoices.

---

## ✅ Features

- Generate invoices in UBL 2.1 format compatible with ZATCA standards
- Apply digital signing with ICV + PIH
- Encode XML content to Base64 and submit through `reporting/single` endpoint
- Automatically parse and display response messages and warnings
- SDK support for the latest .NET Core 8 environment

---

## 🧰 Requirements

- [.NET Core 8 SDK](https://dotnet.microsoft.com)
- Valid digital signing certificate (CSR)
- Developer account on ZATCA’s portal
- Authorized access to send simplified invoices via API

---

## ⚙️ Usage

1. Prepare the invoice XML file and place it inside the `Data` folder
2. Apply cryptographic signing and generate QR Code
3. Run the app to:
   - Encode the invoice into Base64
   - Construct and send the POST request to ZATCA
   - Display the result and any business rule warnings

```bash
dotnet run


📚 Technical Notes
App is designed exclusively for .NET Core 8 (not compatible with .NET Framework)

Easily extendable to cover the Clearance phase or archive solutions

Adaptable for use in Web APIs, microservices, or cloud-hosted functions

✨ Author
Developed by Mustafa Hassaan
