namespace OtakuNest.Common.Services.Caching
{
    public interface IRedisCacheService
    {
        Task<T?> GetDataAsync<T>(string key);
        Task SetDataAsync<T>(string key, T data, TimeSpan? expiration = null);
        Task RemoveDataAsync(string key);

        Task AddToSetAsync(string setKey, string value);
        Task<IEnumerable<string>> GetSetMembersAsync(string setKey);
        Task ClearSetAsync(string setKey);
    }
}
