using Passenger.AgentMode.Issues;

namespace Passenger.UnitTests.AgentMode;

/// <summary>Agent mode's issue and milestone files as text: what is read is what is written, a change rewrites only what
/// it changes (the person's own keys, comments and line breaks kept), and a file the person broke by hand says the line and
/// why, never throws.</summary>
public class IssueFileTests
{
    private const string Written = "---\nid: 15\ntitle: Fix price rounding\nstate: backlog\nmilestone: 0.5\n# the person's own note\npriority: high\n---\n\n"
        + "## What it's for\nTotals are a cent off.\n\n## Done when\n- [ ] Totals match to the cent\n\n## Files it may touch\n- src/Pricing/**\n\n"
        + "## Notes\n```\n## not a heading\n```\n";

    private const string Listed = "---\ntitle: 0.5\n---\n\n## Goal\nCheckout totals are right.\n\n## Issues\n1. #15 Fix price rounding\n2. #16 Show tax\n\n## Later\nIdeas.\n";

    [Fact]
    public void An_issue_reads_as_written_and_a_change_rewrites_only_what_it_changes() => Cases.All(
        ("an issue reads its keys and sections, a heading inside code being code", () =>
        {
            Issue issue = IssueFile.Read(Written).Sure;
            Assert.Equal((15, "Fix price rounding", IssueState.Backlog), (issue.Id, issue.Title, issue.State));
            Assert.Equal("0.5", issue.Milestone);
            Assert.Equal("- [ ] Totals match to the cent", issue.Section(IssueFile.DoneWhen));
            Assert.Equal("```\n## not a heading\n```", issue.Section(IssueFile.Notes));
        }),
        ("a change of state rewrites its value alone, the person's own key and comment kept", () =>
        {
            Issue moved = IssueFile.WithState(IssueFile.Read(Written).Sure, IssueState.PlanWaiting);
            Assert.Equal(Written.Replace("state: backlog", "state: plan-waiting", StringComparison.Ordinal), moved.Text);
        }),
        ("a file's Windows line breaks are kept by a change and by a section written", () =>
        {
            string windows = Written.ReplaceLineEndings("\r\n");
            Issue issue = IssueFile.WithSection(IssueFile.WithTitle(IssueFile.Read(windows).Sure, "Round once"), IssueFile.DoneWhen, "- [ ] One\n- [ ] Two");
            Assert.True(!issue.Text.Replace("\r\n", "", StringComparison.Ordinal).Contains('\n', StringComparison.Ordinal), $"A lone line feed came into a file of Windows line breaks:\n{issue.Text}");
            Assert.Equal("Round once", issue.Title);
            Assert.Equal("- [ ] One\r\n- [ ] Two", issue.Section(IssueFile.DoneWhen));
            Assert.Equal("Totals are a cent off.", issue.Section(IssueFile.WhatItsFor));
        }),
        ("a section the file lacks is added at its end, a key it lacks before the closing ---", () =>
        {
            Issue bare = IssueFile.Read("---\nid: 3\ntitle: Bare\nstate: draft\n---\n").Sure;
            Issue noted = IssueFile.WithMilestone(IssueFile.WithSection(bare, IssueFile.Notes, "Mine."), "0.6");
            Assert.Equal("---\nid: 3\ntitle: Bare\nstate: draft\nmilestone: 0.6\n---\n\n## Notes\nMine.\n", noted.Text);
        }),
        ("a new issue is in the backlog with its four sections, named by its id and title", () =>
        {
            Issue made = IssueFile.New(7, "Show tax per line!", null, "\n");
            Assert.Equal((7, IssueState.Backlog), (made.Id, made.State));
            Assert.Null(made.Milestone);
            Assert.Equal("", made.Section(IssueFile.FilesItMayTouch));
            Assert.Equal("7-show-tax-per-line.md", IssueFile.FileName(made.Id, made.Title));
        }));

    [Fact]
    public void A_file_broken_by_hand_says_its_line_and_why() => Cases.Each<(string Text, int Line, string Says)>(
    [
        ("id: 1\n", 1, "does not begin with a ---"),
        ("---\nid: 1\ntitle: A\nstate: backlog\n", 4, "never closed"),
        ("---\nid: 1\njust words\n---\n", 3, "not a \"key: value\" line"),
        ("---\nid: 1\nid: 2\n---\n", 3, "written twice, on lines 2 and 3"),
        ("---\nid: fifteen\ntitle: A\nstate: backlog\n---\n", 2, "whole number"),
        ("---\nid: 1\nstate: backlog\n---\n", 4, "no title"),
        ("---\nid: 1\ntitle: A\nstate: waiting\n---\n", 4, "none of draft, backlog"),
    ], row =>
    {
        FileRead<Issue> read = IssueFile.Read(row.Text);
        Assert.True(read.Value is null && read.Line == row.Line && read.Why!.Contains(row.Says, StringComparison.Ordinal),
            $"A broken file read as line {read.Line}, \"{read.Why}\", where the person is to be told line {row.Line}, \"{row.Says}\"");
    });

    [Fact]
    public void A_milestone_keeps_its_issues_order_in_its_list_alone() => Cases.All(
        ("its Issues list gives the order, the sections round it kept", () =>
        {
            Milestone milestone = MilestoneFile.Read(Listed).Sure;
            Assert.Equal("0.5", milestone.Title);
            Assert.Equal<int>([15, 16], milestone.Order);
            Assert.Equal("Checkout totals are right.", milestone.Section(MilestoneFile.Goal));
        }),
        ("a new order writes the list again and nothing else", () =>
        {
            Milestone moved = MilestoneFile.WithOrder(MilestoneFile.Read(Listed).Sure, [(16, "Show tax"), (15, "Fix price rounding"), (18, "Round in one place")]);
            Assert.Equal<int>([16, 15, 18], moved.Order);
            Assert.Equal(Listed.Replace("1. #15 Fix price rounding\n2. #16 Show tax\n", "1. #16 Show tax\n2. #15 Fix price rounding\n3. #18 Round in one place\n",
                StringComparison.Ordinal), moved.Text);
        }),
        ("a line of the list that names no issue, or one listed twice, says its line", () =>
        {
            FileRead<Milestone> words = MilestoneFile.Read("---\ntitle: 1\n---\n## Issues\n1. #4 A\nsomething\n");
            FileRead<Milestone> twice = MilestoneFile.Read("---\ntitle: 1\n---\n## Issues\n- #4 A\n* #4 again\n");
            Assert.Equal((6, 6), (words.Line, twice.Line));
            Assert.Contains("listed twice", twice.Why, StringComparison.Ordinal);
        }),
        ("a new milestone has an empty Goal and Issues", () =>
        {
            Milestone made = MilestoneFile.New("0.6", "\n");
            Assert.Equal(("0.6", 0), (made.Title, made.Order.Count));
            Assert.Equal("", made.Section(MilestoneFile.Goal));
        }));
}
