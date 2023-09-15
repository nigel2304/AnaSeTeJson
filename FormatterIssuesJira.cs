using static IssuesJiraModel;

public class FormatterIssuesJira
{
    const string _YES = "Sim";
    const string _NO = "Não";
    const string _STATUS = "status";
    const string _SPRINT = "Sprint";
    const string _BACKLOG = "Backlog";
    const string _FORMATDATE = "yyyy-MM-dd";

    // Return diff dates just work days
    private static int GetWorkingDays(DateTime dateFrom, DateTime dateTo)
    {
        var dayDifference = (int)dateTo.Subtract(dateFrom).TotalDays;
        return Enumerable
            .Range(1, dayDifference)
            .Select(x => dateFrom.AddDays(x))
            .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
    }

    // Return sprint list 
    private static List<string> GetSprintsIssues(IOrderedEnumerable<Histories> itemIssuesChangelogHistories)
    {
        var sprintList = new List<string>();
        var itemIssuesChangelogHistoriesFiltered = itemIssuesChangelogHistories.Where(x => x.items.Any(x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString)));

        itemIssuesChangelogHistoriesFiltered.ToList().ForEach(x =>
        {
            var sprintIssue = x.items.FirstOrDefault(x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString));
            var sprintName = sprintIssue?.toString;
            if (!string.IsNullOrEmpty(sprintName) && sprintList.IndexOf(sprintName) == -1)   
                sprintList.Add(sprintName);

        });

        return sprintList;
    }

    // Return real date and status that issue developing in sprint
    private static Tuple<DateTime?, string>? GetDateAndStatusAfterReplanning(IOrderedEnumerable<Histories> itemIssuesChangelogHistories, string replanning)
    {
        if (replanning != _YES)
            return null;

        var dateAndStatusReplanning = itemIssuesChangelogHistories.FirstOrDefault(x => x.items.Any(x => x.field == _STATUS && x.fromString != _BACKLOG && x.fromString == x.toString));

        var dateReplanning = dateAndStatusReplanning?.created;
        var statusReplanning = dateAndStatusReplanning?.items.LastOrDefault()?.toString;

        return (!string.IsNullOrEmpty(dateReplanning) && !string.IsNullOrEmpty(statusReplanning)) ? 
                        new Tuple<DateTime?, string>(DateTime.SpecifyKind(Convert.ToDateTime(dateReplanning), DateTimeKind.Utc), statusReplanning) 
                        : null;
    }

    // Build object with history status
    public List<IssuesResult> GetIssuesResult(IssuesJira issuesJira)
    {
        var issuesResultList = new List<IssuesResult>();  

        foreach(var itemIssues in issuesJira.issues.OrderBy(x => x.id))
        {
            var issuesResult = new IssuesResult
            {
                Id = itemIssues?.id,
                Key = itemIssues?.key,
                Summary = itemIssues?.fields?.summary,
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
            string dateChangeStatusOld = "0";
    
            // Build history status issue and cycletimes
            var itemIssuesChangelogHistoriesFiltered = itemIssuesChangelogHistories.Where(x => x.items.Any(x => x.field == _STATUS && x.fromString != x.toString));
            foreach (var itemHistories in itemIssuesChangelogHistoriesFiltered)
            {

                //Get only items with status diff    
                var itemsStatus = itemHistories?.items.Where(x => x.field == _STATUS && x.fromString != x.toString);
                if (itemsStatus == null || itemsStatus.Count() == 0)
                    continue;

                // Prepare dates to calculate cycletimes
                var dateChangeStatus = DateTime.SpecifyKind(Convert.ToDateTime(itemHistories?.created), DateTimeKind.Utc);
                var dateFrom = (dateChangeStatusOld != "0") ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;
                var dateTo = Convert.ToDateTime(dateChangeStatus.ToString(_FORMATDATE));

                var dateChangeStatusAfterReplanning = (isUseDateAfterReplanning && dateAfterReplanning.HasValue) ? dateAfterReplanning.Value : dateChangeStatus;
                var dateToAfterReplanning = Convert.ToDateTime(dateChangeStatusAfterReplanning.ToString(_FORMATDATE));

                //Set object to issues history and calculate cycletimes
                var issuesResultHistories = new IssuesResultHistories
                {

                    UserKey = itemHistories?.author?.name,
                    UserName = itemHistories?.author?.displayName,

                    DateChangeStatus = dateChangeStatus.ToString(_FORMATDATE),
                    CycleTime = (dateFrom != DateTime.MinValue) ? (int)dateTo.Subtract(dateFrom).TotalDays : 0,
                    CycleTimeWorkDays = (dateFrom != DateTime.MinValue) ? GetWorkingDays(dateFrom, dateTo) : 0,

                    CycleTimeAfterReplanning = (dateFrom != DateTime.MinValue) ? (int)dateToAfterReplanning.Subtract(dateFrom).TotalDays : 0,
                    CycleTimeWorkDaysAfterReplanning = (dateFrom != DateTime.MinValue) ? GetWorkingDays(dateFrom, dateToAfterReplanning) : 0,

                };

                foreach (var items in itemsStatus)
                {
                    issuesResultHistories.FromStatus = items.fromString;
                    issuesResultHistories.ToStatus = items.toString;
                }

                isUseDateAfterReplanning = dateAfterReplanning.HasValue && issuesResultHistories.ToStatus == statusAfterReplanning && DateTime.Compare(dateFrom, dateAfterReplanning.Value) < 0;

                issuesResult.IssuesResultHistories.Add(issuesResultHistories);

                dateChangeStatusOld = dateChangeStatus.ToString(_FORMATDATE);
            }

            issuesResultList.Add(issuesResult);
        }
        return issuesResultList;
    }

}