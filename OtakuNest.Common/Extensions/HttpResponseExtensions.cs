using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OtakuNest.Common.Helpers;

namespace OtakuNest.Common.Extensions
{
    public static class HttpResponseExtensions
    {
        public static void AddPaginationHeader<T>(this HttpResponse response, PagedList<T> pagedList)
        {
            var paginationHeader = new
            {
                pagedList.TotalCount,
                pagedList.PageSize,
                pagedList.CurrentPage,
                pagedList.TotalPages,
                pagedList.HasNext,
                pagedList.HasPrevious
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            response.Headers.Append("Pagination", JsonSerializer.Serialize(paginationHeader, options));
            response.Headers.Append("Access-Control-Expose-Headers", "Pagination");
        }
    }
}
