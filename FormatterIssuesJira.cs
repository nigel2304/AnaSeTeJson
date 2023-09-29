using static IssuesJiraModel;

public class FormatterIssuesJira
{
    const string _YES = "Sim";
    const string _NO = "Não";
    const string _STATUS = "status";
    const string _SPRINT = "Sprint";
    const string _BACKLOG = "Backlog";
    const string _DONE = "Finalizado";
    const string _FORMATDATE = "yyyy-MM-dd";

    // Return expression issues that change status
    Func<Items, bool> transictionStatus = x => x.field == _STATUS && x.fromString != x.toString;

    // Return expression issues that change status diff done
    Func<Items, bool> transictionStatusNoDone = x => x.field == _STATUS && x.fromString != x.toString && x.toString != _DONE;

    // Return expression issues that change status diff backlog
    Func<Items, bool> transictionStatusNoBacklog = x => x.field == _STATUS && x.fromString != _BACKLOG && x.fromString == x.toString;

    // Return expression to sprints
    Func<Items, bool> expressionSprints = x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString);

    // Return diff dates just work days
    private int GetWorkingDays(DateTime dateFrom, DateTime dateTo)
    {
        var dayDifference = (int)dateTo.Subtract(dateFrom).TotalDays;
        return Enumerable
            .Range(1, dayDifference)
            .Select(x => dateFrom.AddDays(x))
            .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
    }

    // Format datetime specifc kind
    private DateTime GetDateTimeSpecificKind(string? dateToConvert)
    {
        return DateTime.SpecifyKind(Convert.ToDateTime(dateToConvert), DateTimeKind.Utc);
    }

    // Return sprint list 
    private List<string> GetSprintsIssues(IOrderedEnumerable<Histories> itemIssuesChangelogHistories)
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
    private int GetTotalSprints(List<Issues> issues)
    {
        var sprintList = new List<string>();
        issues.Select(x => x.changelog.histories.OrderBy(x => x.created)).ToList().ForEach(x => 
        {
            sprintList.AddRange(GetSprintsIssues(x));
        });
        
        return sprintList.Distinct().Count();
    }

    // Return CycleTime
    private int GetCycletime(DateTime dateFrom, DateTime dateTo, bool isWorkday = false)
    {
        int cycleTime = 0;        
        if (dateFrom != DateTime.MinValue)
            cycleTime = !isWorkday ? (int)dateTo.Subtract(dateFrom).TotalDays : GetWorkingDays(dateFrom, dateTo);

       return cycleTime;
    }

    // Return real date and status that issue developing in sprint
    private Tuple<DateTime?, string>? GetDateAndStatusAfterReplanning(IOrderedEnumerable<Histories> itemIssuesChangelogHistories, string replanning)
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

    //Create and build issues histories 
    private IssuesResultHistories GetIssuesResultHistories(Histories? itemHistories, IEnumerable<Items> itemsStatus, 
            bool isUseDateAfterReplanning, DateTime? dateAfterReplanning, string? dateChangeStatusOld)
    {
        // Prepare dates to calculate cycletimes
        var dateChangeStatus = GetDateTimeSpecificKind(itemHistories?.created);
        var dateFrom = (!string.IsNullOrEmpty(dateChangeStatusOld)) ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;
        var dateTo = Convert.ToDateTime(dateChangeStatus.ToString(_FORMATDATE));

        var dateChangeStatusAfterReplanning = (isUseDateAfterReplanning && dateAfterReplanning.HasValue) ? dateAfterReplanning.Value : dateChangeStatus;
        var dateToAfterReplanning = Convert.ToDateTime(dateChangeStatusAfterReplanning.ToString(_FORMATDATE));

        //Set object to issues history and calculate cycletimes
        var issuesResultHistories = new IssuesResultHistories
        {

            UserKey = itemHistories?.author?.name,
            UserName = itemHistories?.author?.displayName,

            DateChangeStatus = dateChangeStatus.ToString(_FORMATDATE),
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

    // Build object with history status
    public List<IssuesResult> GetIssuesResult(IssuesJira issuesJira)
    {
        var issuesResultList = new List<IssuesResult>();  
    
        var avgStoryPointDone = issuesJira.issues.Sum(x => x.fields.customfield_16702) / GetTotalSprints(issuesJira.issues);

        foreach(var itemIssues in issuesJira.issues.OrderBy(x => x.id))
        {
            bool updateStoryPointFields = true;
            
            var issuesResult = new IssuesResult
            {
                Id = itemIssues?.id,
                Key = itemIssues?.key,
                Summary = itemIssues?.fields?.summary,
                Assigned = itemIssues?.fields?.assignee?.displayName,
                DateResolved = GetDateTimeSpecificKind(itemIssues?.fields?.resolutiondate).ToString(_FORMATDATE),
                AvgStoryPointDone = avgStoryPointDone
            };

            if (itemIssues == null)
                continue;

            // Get sprints by issue and check if had replanning
            var itemIssuesChangelogHistories = itemIssues.changelog.histories.OrderBy(x => x.created);
            var sprintList = GetSprintsIssues(itemIssuesChangelogHistories);
            issuesResult.Sprint = sprintList?.LastOrDefault();
            issuesResult.Replanning = sprintList?.Count > 1 ? _YES : _NO;
            sprintList?.ForEach(x =>
            {
                issuesResult.HistorySprint = string.IsNullOrEmpty(issuesResult.HistorySprint) ? x : issuesResult.HistorySprint + " / " + x;
            });

            // Get date ans status replanning, otherwise real date and status issue devlop in sprint
            var dateAndStatusAfterReplanning = GetDateAndStatusAfterReplanning(itemIssuesChangelogHistories, issuesResult.Replanning);
            DateTime? dateAfterReplanning = null;
            string statusAfterReplanning = string.Empty;
            if (dateAndStatusAfterReplanning != null)
            {
                dateAfterReplanning = dateAndStatusAfterReplanning.Item1.HasValue ? dateAndStatusAfterReplanning.Item1.Value : null;
                statusAfterReplanning = dateAndStatusAfterReplanning.Item2;
                issuesResult.DateReplanning = dateAfterReplanning.HasValue ? dateAfterReplanning.Value.ToString(_FORMATDATE) : string.Empty;
            }
    
            bool isUseDateAfterReplanning = false;
            string dateChangeStatusOld = string.Empty;
    
            // Get history status issue
            var itemIssuesChangelogHistoriesFiltered = itemIssuesChangelogHistories.Where(x => x.items.Any(transictionStatus));
            
            // Get last history status open issue
            var itemIssuesLastChangelogHistories = itemIssuesChangelogHistoriesFiltered.LastOrDefault(x => x.items.Any(transictionStatus))?
                    .items.LastOrDefault(transictionStatusNoDone);

            // Build history status issue and cycletimes
            foreach (var itemHistories in itemIssuesChangelogHistoriesFiltered)
            {

                //Get only items with status diff    
                var itemsStatus = itemHistories?.items.Where(transictionStatus);
                if (itemsStatus == null || itemsStatus.Count() == 0)
                    continue;

                var dateFrom = !string.IsNullOrEmpty(dateChangeStatusOld) ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;

                //Create and build issues histories 
                var issuesResultHistories = GetIssuesResultHistories(itemHistories, itemsStatus, isUseDateAfterReplanning, dateAfterReplanning, dateChangeStatusOld);
                if (updateStoryPointFields)
                {
                    issuesResultHistories.StoryPoint = itemIssues?.fields?.customfield_16701;
                    issuesResultHistories.StoryPointDone = itemIssues?.fields?.customfield_16702;    
                }

                isUseDateAfterReplanning = dateAfterReplanning.HasValue && issuesResultHistories.ToStatus == statusAfterReplanning && DateTime.Compare(dateFrom, dateAfterReplanning.Value) < 0;
                updateStoryPointFields = false;

                issuesResult.IssuesResultHistories.Add(issuesResultHistories);

                dateChangeStatusOld = !string.IsNullOrEmpty(issuesResultHistories.DateChangeStatus) ? issuesResultHistories.DateChangeStatus : string.Empty;

                //If issues is open and last record so calculate cycletime it
                if (itemIssuesLastChangelogHistories != null && itemIssuesLastChangelogHistories.toString == issuesResultHistories.ToStatus)
                {
                    dateFrom = Convert.ToDateTime(issuesResultHistories.DateChangeStatus);
                    var issuesLastResultHistories = new IssuesResultHistories
                    {

                        UserKey = issuesResultHistories.UserKey,
                        UserName = issuesResultHistories.UserName,

                        DateChangeStatus = issuesResultHistories.DateChangeStatus,
                        CycleTime = GetCycletime(dateFrom, DateTime.UtcNow),
                        CycleTimeWorkDays = GetCycletime(dateFrom, DateTime.UtcNow, true),

                        FromStatus = issuesResultHistories.ToStatus,
                        ToStatus = issuesResultHistories.ToStatus,

                        StoryPoint = issuesResultHistories.StoryPoint,
                        StoryPointDone = issuesResultHistories.StoryPointDone    
                    };
                    issuesLastResultHistories.CycleTimeAfterReplanning = issuesLastResultHistories.CycleTimeAfterReplanning;
                    issuesLastResultHistories.CycleTimeWorkDaysAfterReplanning = issuesLastResultHistories.CycleTimeWorkDaysAfterReplanning;

                    issuesResult.IssuesResultHistories.Add(issuesLastResultHistories);
                }
            }

            issuesResultList.Add(issuesResult);
        }
        return issuesResultList;
    }

}