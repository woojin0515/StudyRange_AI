namespace StudyRange.Domain.ValueObjects;

public readonly record struct PageRange
{
    public int StartPage { get; }
    public int EndPage { get; }

    public PageRange(int startPage, int endPage)
    {
        if (startPage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startPage), "Start page must be greater than 0.");
        }

        if (endPage < startPage)
        {
            throw new ArgumentOutOfRangeException(nameof(endPage), "End page must be greater than or equal to start page.");
        }

        StartPage = startPage;
        EndPage = endPage;
    }
}
