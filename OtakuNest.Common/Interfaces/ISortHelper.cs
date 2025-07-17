﻿namespace OtakuNest.Common.Interfaces
{
    public interface ISortHelper<T>
    {
        IQueryable<T> ApplySort(IQueryable<T> entities, string? orderByQueryString);
    }
}
