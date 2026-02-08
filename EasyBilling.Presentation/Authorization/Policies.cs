namespace EasyBilling.Presentation.Authorization
{
    public static class Policies
    {
        public const string AdminOnly = "AdminOnly";
        public const string UserOnly = "UserOnly";
        public const string CanCreateInvoice = "CanCreateInvoice";
        public const string CanUseEFactura = "CanUseEFactura";
        public const string HasActiveMembership = "HasActiveMembership";
    }
}
