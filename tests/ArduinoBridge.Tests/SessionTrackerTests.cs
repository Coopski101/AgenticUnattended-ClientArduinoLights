namespace ArduinoBridge.Tests;

public class SessionTrackerTests
{
    private static string Json(string sessionId) =>
        $$"""{"sessionId":"{{sessionId}}"}""";

    [Fact]
    public void InitialState_ReturnsC()
    {
        var tracker = new SessionTracker();
        Assert.Equal("C", tracker.ApplyEvent("Unknown", "s1"));
    }

    [Fact]
    public void SingleWaiting_ReturnsW()
    {
        var tracker = new SessionTracker();
        Assert.Equal("W", tracker.ProcessEvent("Waiting", Json("s1")));
    }

    [Fact]
    public void SingleDone_ReturnsD()
    {
        var tracker = new SessionTracker();
        Assert.Equal("D", tracker.ProcessEvent("Done", Json("s1")));
    }

    [Fact]
    public void Clear_ReturnsC()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        Assert.Equal("C", tracker.ProcessEvent("Clear", Json("s1")));
    }

    [Fact]
    public void SessionEnded_RemovesSession()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        Assert.Equal("C", tracker.ProcessEvent("SessionEnded", Json("s1")));
    }

    [Fact]
    public void WaitingTakesPriorityOverDone()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        Assert.Equal("W", tracker.ProcessEvent("Waiting", Json("s2")));
    }

    [Fact]
    public void DoneTakesPriorityOverClear()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Clear", Json("s1"));
        Assert.Equal("D", tracker.ProcessEvent("Done", Json("s2")));
    }

    [Fact]
    public void RemovingWaitingSession_FallsBackToDone()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        tracker.ProcessEvent("Waiting", Json("s2"));
        Assert.Equal("D", tracker.ProcessEvent("SessionEnded", Json("s2")));
    }

    [Fact]
    public void RemovingAllSessions_ReturnsC()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        tracker.ProcessEvent("Waiting", Json("s2"));
        tracker.ProcessEvent("SessionEnded", Json("s2"));
        Assert.Equal("C", tracker.ProcessEvent("SessionEnded", Json("s1")));
    }

    [Fact]
    public void SameSessionTransitions_WaitingToDone()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Waiting", Json("s1"));
        Assert.Equal("D", tracker.ProcessEvent("Done", Json("s1")));
    }

    [Fact]
    public void SameSessionTransitions_DoneToClear()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        Assert.Equal("C", tracker.ProcessEvent("Clear", Json("s1")));
    }

    [Fact]
    public void MultipleSessionsAllWaiting_ReturnsW()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Waiting", Json("s1"));
        Assert.Equal("W", tracker.ProcessEvent("Waiting", Json("s2")));
    }

    [Fact]
    public void MultipleSessionsAllDone_ReturnsD()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        Assert.Equal("D", tracker.ProcessEvent("Done", Json("s2")));
    }

    [Fact]
    public void MultipleSessionsMixed_WaitingWins()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        tracker.ProcessEvent("Clear", Json("s2"));
        Assert.Equal("W", tracker.ProcessEvent("Waiting", Json("s3")));
    }

    [Fact]
    public void ClearingWaitingSession_OtherDoneRemains()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Done", Json("s1"));
        tracker.ProcessEvent("Waiting", Json("s2"));
        Assert.Equal("D", tracker.ProcessEvent("Clear", Json("s2")));
    }

    [Fact]
    public void UnknownEventType_DoesNotCrash()
    {
        var tracker = new SessionTracker();
        Assert.Equal("C", tracker.ProcessEvent("SomethingNew", Json("s1")));
    }

    [Fact]
    public void SessionEndedForNonexistentSession_DoesNotCrash()
    {
        var tracker = new SessionTracker();
        Assert.Equal("C", tracker.ProcessEvent("SessionEnded", Json("nonexistent")));
    }

    [Fact]
    public void RepeatedEventsForSameSession_OverwritesState()
    {
        var tracker = new SessionTracker();
        tracker.ProcessEvent("Waiting", Json("s1"));
        tracker.ProcessEvent("Waiting", Json("s1"));
        Assert.Equal("W", tracker.ProcessEvent("Waiting", Json("s1")));
    }

    [Fact]
    public void FullLifecycle_WaitingDoneClearEnd()
    {
        var tracker = new SessionTracker();
        Assert.Equal("W", tracker.ProcessEvent("Waiting", Json("s1")));
        Assert.Equal("D", tracker.ProcessEvent("Done", Json("s1")));
        Assert.Equal("C", tracker.ProcessEvent("Clear", Json("s1")));
        Assert.Equal("C", tracker.ProcessEvent("SessionEnded", Json("s1")));
    }

    [Fact]
    public void ProcessEvent_ParsesSessionIdFromJson()
    {
        var tracker = new SessionTracker();
        string result = tracker.ProcessEvent("Waiting", """{"sessionId":"abc-123","extra":"data"}""");
        Assert.Equal("W", result);
    }
}
