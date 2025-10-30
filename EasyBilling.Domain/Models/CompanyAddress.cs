using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyBilling.Domain.Models
{
    public class CompanyAddress
    {
        [Required]
        [StringLength(200)]
        public string AddressLine1 { get; set; }

        [StringLength(200)]
        public string? AddressLine2 { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [Required]
        [StringLength(100)]
        public string Province { get; set; }

        [StringLength(20)]
        public string PostalCode { get; set; }
    }
}
