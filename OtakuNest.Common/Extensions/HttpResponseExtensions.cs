using Microsoft.AspNetCore.Http;
using System.Text.Json;
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

            response.Headers.Add("X-Pagination", JsonSerializer.Serialize(paginationHeader, options));
            response.Headers.Add("Access-Control-Expose-Headers", "X-Pagination");
        }
    }
}
