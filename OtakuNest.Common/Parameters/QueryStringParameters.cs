namespace OtakuNest.Common.Parameters
{
    public abstract class QueryStringParameters
    {
        private const int MaxPageSize = 50;
        private int _pageSize = 10;
        private int _pageNumber = 1;

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("PageNumber must be greater than 0");
                _pageNumber = value;
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("PageSize must be greater than 0");
                _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
            }
        }

        public string? OrderBy { get; set; } = "Id";
    }
}
