using EasyBilling.Domain.Models;

namespace EasyBilling.ANAFIntegration.EFactura
{
    public interface IEFacturaXmlGenerator
    {
        /// <summary>
        /// Generates UBL 2.1 XML for ANAF E-Factura from an Invoice entity
        /// </summary>
        /// <param name="invoice">The invoice to convert to XML</param>
        /// <returns>XML string in UBL 2.1 format conforming to RO_CIUS specification</returns>
        string GenerateXml(Invoice invoice);

        /// <summary>
        /// Generates UBL 2.1 XML for ANAF E-Factura and saves it to a file
        /// </summary>
        /// <param name="invoice">The invoice to convert to XML</param>
        /// <param name="filePath">Path where the XML file should be saved</param>
        void GenerateXmlFile(Invoice invoice, string filePath);
    }
}
