using static IssuesJiraModel;
using static FormatterIssuesJiraCommon;

public class FormatterIssuesJira
{
    // Return expression issues that change status
    Func<Items, bool> transictionStatus = x => x.field == _STATUS && x.fromString != x.toString;

    // Return expression issues that change status diff done
    Func<Items, bool> transictionStatusNoDone = x => x.field == _STATUS && x.fromString != x.toString && x.toString != _DONE;

    // Return has story points done
    Func<IssuesResultHistories, bool> expressionHasStoryPointsDone = x => x.StoryPointDone.HasValue && x.StoryPointDone.Value > 0;

     // Build object with history status
    public List<IssuesResult> GetIssuesResult(IssuesJira issuesJira)
    {
        var formatterIssuesJiraUtis = new FormatterIssuesJiraUtis();

        var issuesResultList = new List<IssuesResult>();  
    
        foreach(var itemIssues in issuesJira.issues.OrderBy(x => x.id))
        {
            bool updateStoryPointFields = true;
            
            var issuesResult = new IssuesResult
            {
                Id = itemIssues?.id,
                Key = itemIssues?.key,
                Summary = itemIssues?.fields?.summary,
                Assigned = itemIssues?.fields?.assignee?.displayName,
                DateResolved = formatterIssuesJiraUtis.GetDateTimeSpecificKind(itemIssues?.fields?.resolutiondate).ToString(_FORMAT_DATE)
            };

            if (itemIssues == null)
                continue;

            // Get sprints by issue and check if had replanning
            var itemIssuesChangelogHistories = itemIssues.changelog.histories.OrderBy(x => x.created);
            var sprintList = formatterIssuesJiraUtis.GetSprintsIssues(itemIssuesChangelogHistories);
            issuesResult.Sprint = sprintList?.LastOrDefault();
            issuesResult.Replanning = sprintList?.Count > 1 ? _YES : _NO;
            sprintList?.ForEach(x =>
            {
                issuesResult.HistorySprint = string.IsNullOrEmpty(issuesResult.HistorySprint) ? x : issuesResult.HistorySprint + " / " + x;
            });

            // Get start and end date sprint
            var startEndDateSprint = formatterIssuesJiraUtis.GetStartEndDateSprint(itemIssues?.fields?.customfield_10105);
            issuesResult.StartDateSprint = formatterIssuesJiraUtis.GetDateTimeSpecificKind(startEndDateSprint?.Item1).ToString(_FORMAT_DATE);
            issuesResult.EndDateSprint = formatterIssuesJiraUtis.GetDateTimeSpecificKind(startEndDateSprint?.Item2).ToString(_FORMAT_DATE);

            // Get date and status replanning, otherwise real date and status issue develop in sprint
            var dateAndStatusAfterReplanning = formatterIssuesJiraUtis.GetDateAndStatusAfterReplanning(itemIssuesChangelogHistories, issuesResult.Replanning);
            DateTime? dateAfterReplanning = null;
            string statusAfterReplanning = string.Empty;
            if (dateAndStatusAfterReplanning != null)
            {
                dateAfterReplanning = dateAndStatusAfterReplanning.Item1.HasValue ? dateAndStatusAfterReplanning.Item1.Value : null;
                statusAfterReplanning = dateAndStatusAfterReplanning.Item2;
                issuesResult.DateReplanning = dateAfterReplanning.HasValue ? dateAfterReplanning.Value.ToString(_FORMAT_DATE) : string.Empty;
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

                // Get only items with status diff    
                var itemsStatus = itemHistories?.items.Where(transictionStatus);
                if (itemsStatus == null || itemsStatus.Count() == 0)
                    continue;

                var dateFrom = !string.IsNullOrEmpty(dateChangeStatusOld) ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;

                // Create and build issues histories 
                var issuesResultHistories = formatterIssuesJiraUtis.GetIssuesResultHistories(itemHistories, itemsStatus, isUseDateAfterReplanning, dateAfterReplanning, dateChangeStatusOld, issuesResult.StartDateSprint, issuesResult.Replanning);
                if (updateStoryPointFields)
                {
                    issuesResultHistories.StoryPoint = itemIssues?.fields?.customfield_16701;
                    issuesResultHistories.StoryPointDone = itemIssues?.fields?.customfield_16702;    
                }

                // Check if issue was add in sprint after started it
                if (!formatterIssuesJiraUtis.DateTimeIsMinValue(Convert.ToDateTime(issuesResult.StartDateSprint)) && issuesResultHistories.FromStatus ==_BACKLOG && issuesResultHistories.ToStatus ==_PLANNED)
                {
                    var dateStartSprintLessDateIssue = Convert.ToDateTime(issuesResult.StartDateSprint).CompareTo(Convert.ToDateTime(issuesResultHistories.DateChangeStatus)) < 0; 
                    issuesResult.AddAfterStartedSprint = dateStartSprintLessDateIssue ? _YES : _NO;
                }

                isUseDateAfterReplanning = dateAfterReplanning.HasValue && issuesResultHistories.ToStatus == statusAfterReplanning && DateTime.Compare(dateFrom, dateAfterReplanning.Value) < 0;
                updateStoryPointFields = false;

                issuesResult.IssuesResultHistories.Add(issuesResultHistories);

                dateChangeStatusOld = !string.IsNullOrEmpty(issuesResultHistories.DateChangeStatus) ? issuesResultHistories.DateChangeStatus : string.Empty;
            }

            // If issues is open and last record so calculate cycletime it
            var issuesResultHistoriesLast = issuesResult.IssuesResultHistories.LastOrDefault();
            if (itemIssuesLastChangelogHistories != null && issuesResultHistoriesLast != null && itemIssuesLastChangelogHistories.toString == issuesResultHistoriesLast.ToStatus)
            {
                var dateFrom = Convert.ToDateTime(issuesResultHistoriesLast.DateChangeStatus);
                var issuesLastResultHistories = new IssuesResultHistories
                {

                    UserKey = issuesResultHistoriesLast.UserKey,
                    UserName = issuesResultHistoriesLast.UserName,

                    DateChangeStatus = issuesResultHistoriesLast.DateChangeStatus,
                    CycleTime = formatterIssuesJiraUtis.GetCycletime(dateFrom, DateTime.UtcNow),
                    CycleTimeWorkDays = formatterIssuesJiraUtis.GetCycletime(dateFrom, DateTime.UtcNow, true),

                    FromStatus = issuesResultHistoriesLast.ToStatus,
                    ToStatus = issuesResultHistoriesLast.ToStatus,

                    StoryPoint = issuesResultHistoriesLast.StoryPoint,
                    StoryPointDone = issuesResultHistoriesLast.StoryPointDone
                };
                dateFrom = isUseDateAfterReplanning && dateAfterReplanning.HasValue ? dateAfterReplanning.Value : Convert.ToDateTime(issuesResultHistoriesLast.DateChangeStatus);
                issuesLastResultHistories.CycleTimeAfterReplanning = formatterIssuesJiraUtis.GetCycletime(dateFrom, DateTime.UtcNow);
                issuesLastResultHistories.CycleTimeWorkDaysAfterReplanning = formatterIssuesJiraUtis.GetCycletime(dateFrom, DateTime.UtcNow, true);

                issuesResult.IssuesResultHistories.Add(issuesLastResultHistories);
            }

            // Check if issue has story point done but hasn't date resolved and set it
            if (formatterIssuesJiraUtis.DateTimeIsMinValue(Convert.ToDateTime(issuesResult.DateResolved)) && issuesResult.IssuesResultHistories.Any(expressionHasStoryPointsDone))
            {
                issuesResult.DateResolved = issuesResult.IssuesResultHistories?.LastOrDefault()?.DateChangeStatus;
            }


            issuesResultList.Add(issuesResult);
        }
        return issuesResultList;
    }

}