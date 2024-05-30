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
                sprintList.Add(FormatSprintName(sprintName));

        });

        return sprintList;
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
		
		var startRealDate = infoSprintJira.Substring(startSubstring, 10);
        var startDate = GetStartDateTime(startRealDate);

		startSubstring = infoSprintJira.IndexOf(_END_DATE) + _END_DATE.Length;
		
		var endDate = infoSprintJira.Substring(startSubstring, 10);

        return Tuple.Create(startDate, endDate);
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
            bool isUseDateAfterReplanning, DateTime? dateAfterReplanning, string? dateChangeStatusOld, string? startDateSprint, string replanning)
    {
        // Prepare dates to calculate cycletimes
        var dateChangeStatus = DateChangeStatus(replanning, itemHistories?.created, startDateSprint);
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
        issuesResultHistories.CycleTimeEqualCycleTimeAfterReplanning = issuesResultHistories.CycleTime.CompareTo(issuesResultHistories.CycleTimeAfterReplanning) == 0 ? _YES : _NO;

        foreach (var items in itemsStatus)
        {
            issuesResultHistories.FromStatus = items.fromString;
            issuesResultHistories.ToStatus = items.toString;
        }

        return issuesResultHistories;
    }

    //Return real start datetime discount holidays
    private string GetStartDateTime(string startDate)
    {
        var startDateTime = GetDateTimeSpecificKind(startDate);

        var year = startDateTime.Year;
        var holidays = new List<DateTime>()
        {
            // New year
            new DateTime(year, 1, 1),
            // Carnival monday
            new DateTime(year, 2, 12),
            // Carnival tuesday
            new DateTime(year, 2, 13),
            // Passion Christ
            new DateTime(year, 3, 29), 
            // Tiradentes
            new DateTime(year, 4, 21),
            // Worker day
            new DateTime(year, 5, 1),
            // Corpus Christ
            new DateTime(year, 5, 30),
            // Independace day
            new DateTime(year, 9, 7),
            // Brazil patron saint
            new DateTime(year, 10, 12),
            // Dead day
            new DateTime(year, 11, 2),
            // Black consciousness day
            new DateTime(year, 11, 20),
            // Mary christmas
            new DateTime(year, 12, 25)
        };

        // Check if start date sprint is holyday
        if (holidays.IndexOf(startDateTime) != -1)
        {
            var incDay = startDateTime.CompareTo(holidays[1]) == 0 ? 2 : 1;
            return startDateTime.AddDays(incDay).ToString(_FORMAT_DATE);
        }
            
        return startDate;
    }

    //Return date change status
    private DateTime DateChangeStatus(string? replanning, string? dateChange, string? startDateSprint)
    {
        if (replanning == _YES)
            return GetDateTimeSpecificKind(dateChange);

        var dateTimeChange = GetDateTimeSpecificKind(dateChange);
        var dateTimeStartSprint = GetDateTimeSpecificKind(startDateSprint);                    

        return dateTimeChange.CompareTo(dateTimeStartSprint) < 0 ? dateTimeStartSprint : dateTimeChange;
    }

       //Return date change status
    public bool DateTimeIsMinValue(DateTime dateTimeCheck)
    {
        return dateTimeCheck.CompareTo(DateTime.MinValue) == 0;
    }

    //Format and return sprint name
    public string FormatSprintName(string sourceSprintName)
    {
        var sprintNewName = sourceSprintName.Replace(_WORDNICHO, string.Empty);
        var sprintNameLessTen = new List<string>()
        {
            "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", 
            "Sprint 6", "Sprint 7", "Sprint 8", "Sprint 9"
        };

        // Check if sprint less ten
        if (sprintNameLessTen.IndexOf(sprintNewName) != -1)
        {
            var numberSprint = sprintNewName.Substring(sprintNewName.Length - 1);
            sprintNewName = sprintNewName.Replace(numberSprint, string.Concat("0", numberSprint));
        }
            
        return sprintNewName;
    }

}