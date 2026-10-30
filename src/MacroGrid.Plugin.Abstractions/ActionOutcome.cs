using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>What happened when an action ran.</summary>
public enum ActionOutcomeKind
{
    /// <summary>The action did what it was asked to do.</summary>
    Success,

    /// <summary>The action could not do it; <see cref="ActionOutcome.Code"/> says why.</summary>
    Failed,

    /// <summary>The request was handed over, but the other side cannot confirm the result.</summary>
    Accepted,
}

/// <summary>Why an action failed. The order is final: new codes are only appended.</summary>
public enum ActionFailureCode
{
    /// <summary>A required setting is empty or the action is not set up yet.</summary>
    NotConfigured,

    /// <summary>The program, device or service the action talks to is not connected.</summary>
    NotConnected,

    /// <summary>The other side or the system did not allow it.</summary>
    PermissionDenied,

    /// <summary>The other side reported an error, or something unexpected went wrong.</summary>
    ProviderError,

    /// <summary>The other side understood the request and refused it.</summary>
    ProviderRejected,

    /// <summary>A setting of this action has a value that cannot be used.</summary>
    InvalidParameter,

    /// <summary>The target (a scene, a file, a sound) does not exist.</summary>
    NotFound,

    /// <summary>There was no answer in time.</summary>
    Timeout,

    /// <summary>The action cannot run right now (no output device, too many waiting runs).</summary>
    Unavailable,
}

/// <summary>
/// The result of one action run. Create it with <see cref="Success"/>, <see cref="Failed"/> or <see cref="Accepted"/>.
/// Rules for authors: do not report <see cref="Success"/> only because a request was sent; wait for the answer, but for a bounded time, and
/// then report <see cref="ActionFailureCode.Timeout"/>; report <see cref="Accepted"/> when the other side cannot confirm; honour the
/// cancellation token; write the message in plain words, with no secret, no address with credentials and no file path.
/// </summary>
public sealed class ActionOutcome
{
    private ActionOutcome(ActionOutcomeKind kind, ActionFailureCode? code, string? message)
    {
        Kind = kind;
        Code = code;
        Message = message;
    }

    public ActionOutcomeKind Kind { get; }

    /// <summary>Only set for <see cref="ActionOutcomeKind.Failed"/>.</summary>
    public ActionFailureCode? Code { get; }

    /// <summary>For a failure, what the person reads (the host supplies a default text when it is empty); for <see cref="ActionOutcomeKind.Accepted"/> an optional note.</summary>
    public string? Message { get; }

    /// <summary>The one shared success value.</summary>
    public static ActionOutcome Success { get; } = new(ActionOutcomeKind.Success, null, null);

    public static ActionOutcome Failed(ActionFailureCode code, string? message = null) => new(ActionOutcomeKind.Failed, code, message);

    public static ActionOutcome Accepted(string? message = null) => new(ActionOutcomeKind.Accepted, null, message);
}

/// <summary>
/// Optional side interface of an <see cref="IActionHandler"/> that reports a result instead of only throwing. When a handler implements it the
/// host calls only <see cref="ExecuteWithOutcomeAsync"/>; <see cref="IActionHandler.ExecuteAsync"/> must still exist, and the recommended
/// body is <c>(await ExecuteWithOutcomeAsync(...)).ThrowIfFailed()</c>. An exception from this method counts as
/// <see cref="ActionFailureCode.ProviderError"/>. A plugin that uses it needs an editor that has this interface (raise <c>minMacroGrid</c>).
/// </summary>
public interface IActionOutcomeHandler
{
    Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken);
}

public static class ActionOutcomeExtensions
{
    /// <summary>Throws <see cref="InvalidOperationException"/> with the outcome's message when it is a failure; does nothing otherwise.</summary>
    public static void ThrowIfFailed(this ActionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Kind == ActionOutcomeKind.Failed)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(outcome.Message) ? outcome.Code?.ToString() : outcome.Message);
    }
}
