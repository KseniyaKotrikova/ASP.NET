
using System.Collections;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models.Customers;
using PromoCodeFactory.WebHost.Models.Preferences;

namespace PromoCodeFactory.WebHost.Mapping;
public static class CustomersMapper
{
    public static CustomerShortResponse ToCustomerShortResponse(Customer customer)
    {
        return new CustomerShortResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.Preferences.Select(p => new PreferenceShortResponse(
                p.Id,
                p.Name
            )).ToList());
    }

}
