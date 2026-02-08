using System;
using System.Collections.Generic;
using System.Text;

namespace EasyBilling.Application.Dtos;

public class PaginationRequest
{
    private int _pageNumber = 1;
    private int _pageSize = 10;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : (value > 100 ? 100 : value);
    }

    public int Skip => (PageNumber - 1) * PageSize;

    public bool IsValid => PageNumber >= 1 && PageSize >= 1 && PageSize <= 100;
}