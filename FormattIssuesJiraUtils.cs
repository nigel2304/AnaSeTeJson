using static IssuesJiraModel;
using static FormatterIssuesJiraCommon;

public class FormatterIssuesJiraUtis
{
    // Return expression issues that change status diff backlog
    Func<Items, bool> transictionStatusNoBacklog = x => x.field == _STATUS && x.fromString != _BACKLOG && x.fromString == x.toString;

    // Return expression to sprints
    Func<Items, bool> expressionSprints = x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString);

    // Return diff dates just work days
    public int GetWorkingDays(DateTime dateFrom, DateTime dateTo)
    {
        var dayDifference = (int)dateTo.Subtract(dateFrom).TotalDays;
        return Enumerable
            .Range(1, dayDifference)
            .Select(x => dateFrom.AddDays(x))
            .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
    }

    // Format datetime specifc kind
    public DateTime GetDateTimeSpecificKind(string? dateToConvert)
    {
        return DateTime.SpecifyKind(Convert.ToDateTime(dateToConvert), DateTimeKind.Utc);
    }

    // Return sprint list 
    public List<string> GetSprintsIssues(IOrderedEnumerable<Histories> itemIssuesChangelogHistories)
    {
        var sprintList = new List<string>();
        var itemIssuesChangelogHistoriesFiltered = itemIssuesChangelogHistories.Where(x => x.items.Any(expressionSprints));

        itemIssuesChangelogHistoriesFiltered.ToList().ForEach(x =>
        {
            var sprintIssue = x.items.FirstOrDefault(x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString));
            var sprintName = sprintIssue?.toString;
            if (!string.IsNullOrEmpty(sprintName) && sprintList.IndexOf(sprintName) == -1)   
                sprintList.Add(sprintName);

        });

        return sprintList;
    }

    // Return total sprints
    public int GetTotalSprints(List<Issues> issues)
    {
        var sprintList = new List<string>();
        issues.Select(x => x.changelog.histories.OrderBy(x => x.created)).ToList().ForEach(x => 
        {
            sprintList.AddRange(GetSprintsIssues(x));
        });
        
        return sprintList.Distinct().Count();
    }

    // Return CycleTime
    public int GetCycletime(DateTime dateFrom, DateTime dateTo, bool isWorkday = false)
    {
        int cycleTime = 0;        
        if (dateFrom != DateTime.MinValue)
            cycleTime = !isWorkday ? (int)dateTo.Subtract(dateFrom).TotalDays : GetWorkingDays(dateFrom, dateTo);

       return cycleTime;
    }

    // Return start date and end date sprint
    public Tuple<string, string>? GetStartEndDateSprint(string? infoSprintJira)
    {
        if (string.IsNullOrEmpty(infoSprintJira))
            return null;

		var startSubstring = infoSprintJira.IndexOf(_START_DATE) + _START_DATE.Length;
		
		var startDate = infoSprintJira.Substring(startSubstring, 10);

		startSubstring = infoSprintJira.IndexOf(_END_DATE) + _END_DATE.Length;
		
		var endDate = infoSprintJira.Substring(startSubstring, 10);

        return new Tuple<string, string>(startDate, endDate);
    }

    // Return real date and status that issue developing in sprint
    public Tuple<DateTime?, string>? GetDateAndStatusAfterReplanning(IOrderedEnumerable<Histories> itemIssuesChangelogHistories, string replanning)
    {
        if (replanning != _YES)
            return null;

        var dateAndStatusReplanning = itemIssuesChangelogHistories.FirstOrDefault(x => x.items.Any(transictionStatusNoBacklog));

        var dateReplanning = dateAndStatusReplanning?.created;
        var statusReplanning = dateAndStatusReplanning?.items.LastOrDefault()?.toString;

        return (!string.IsNullOrEmpty(dateReplanning) && !string.IsNullOrEmpty(statusReplanning)) ? 
                        new Tuple<DateTime?, string>(GetDateTimeSpecificKind(dateReplanning), statusReplanning) 
                        : null;
    }

    // Create and build issues histories 
    public IssuesResultHistories GetIssuesResultHistories(Histories? itemHistories, IEnumerable<Items> itemsStatus, 
            bool isUseDateAfterReplanning, DateTime? dateAfterReplanning, string? dateChangeStatusOld)
    {
        // Prepare dates to calculate cycletimes
        var dateChangeStatus = GetDateTimeSpecificKind(itemHistories?.created);
        var dateFrom = (!string.IsNullOrEmpty(dateChangeStatusOld)) ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;
        var dateTo = Convert.ToDateTime(dateChangeStatus.ToString(_FORMAT_DATE));

        var dateChangeStatusAfterReplanning = (isUseDateAfterReplanning && dateAfterReplanning.HasValue) ? dateAfterReplanning.Value : dateChangeStatus;
        var dateToAfterReplanning = Convert.ToDateTime(dateChangeStatusAfterReplanning.ToString(_FORMAT_DATE));

        // Set object to issues history and calculate cycletimes
        var issuesResultHistories = new IssuesResultHistories
        {

            UserKey = itemHistories?.author?.name,
            UserName = itemHistories?.author?.displayName,

            DateChangeStatus = dateChangeStatus.ToString(_FORMAT_DATE),
            CycleTime = GetCycletime(dateFrom, dateTo),
            CycleTimeWorkDays = GetCycletime(dateFrom, dateTo, true),

            CycleTimeAfterReplanning = GetCycletime(dateFrom, dateToAfterReplanning),
            CycleTimeWorkDaysAfterReplanning = GetCycletime(dateFrom, dateToAfterReplanning, true),

        };

        foreach (var items in itemsStatus)
        {
            issuesResultHistories.FromStatus = items.fromString;
            issuesResultHistories.ToStatus = items.toString;
        }

        return issuesResultHistories;
    }

}