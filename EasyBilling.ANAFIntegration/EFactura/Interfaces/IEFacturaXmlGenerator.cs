using EasyBilling.Domain.Models;

namespace EasyBilling.ANAFIntegration.EFactura.Interfaces
{
    public interface IEFacturaXmlGenerator
    {
        string GenerateXml(Invoice invoice);
        void GenerateXmlFile(Invoice invoice, string filePath);
    }
}
