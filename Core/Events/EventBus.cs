namespace SevenSpices.Core.Events;

/// <summary>
/// 轻量事件总线：核心系统发布事件，表现层 / 伙伴系统等订阅刷新。
/// <para>
/// 订阅分两类：
/// <list type="bullet">
/// <item>精确订阅 <see cref="Subscribe{T}(Action{T})"/>：只接收运行时类型恰好为 <c>T</c> 的事件。</item>
/// <item>全局订阅 <see cref="Subscribe(Action{GameEvent})"/>：接收所有事件，用于表现层统一刷新。</item>
/// </list>
/// <see cref="Publish{T}"/> 先派发给精确类型订阅者，再派发给全局订阅者。
/// 无订阅者时不报错；派发使用快照，允许订阅者在回调中退订。
/// 订阅者异常被逐个隔离（见 <see cref="InvokeSafely"/>），不会中断其余订阅者或核心流程。
/// 纯 C#、无 Godot 依赖、无常量静态状态（每个实例独立）。
/// </para>
/// </summary>
public sealed class EventBus
{
    private sealed record Subscription(Delegate Handler, Action<GameEvent> Invoke);

    private readonly Dictionary<Type, List<Subscription>> _typedHandlers = new();
    private readonly List<Action<GameEvent>> _allHandlers = new();

    /// <summary>按具体运行时类型订阅（运行时类型必须恰好为 <typeparamref name="T"/>）。</summary>
    public void Subscribe<T>(Action<T> handler) where T : GameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_typedHandlers.TryGetValue(typeof(T), out var list))
        {
            list = new List<Subscription>();
            _typedHandlers[typeof(T)] = list;
        }

        list.Add(new Subscription(handler, e => handler((T)e)));
    }

    /// <summary>订阅所有事件（表现层统一刷新入口）。</summary>
    public void Subscribe(Action<GameEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _allHandlers.Add(handler);
    }

    /// <summary>取消一个精确类型订阅。</summary>
    public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_typedHandlers.TryGetValue(typeof(T), out var list))
            return;

        list.RemoveAll(s => s.Handler.Equals(handler));
        if (list.Count == 0)
            _typedHandlers.Remove(typeof(T));
    }

    /// <summary>取消一个全局订阅。</summary>
    public void Unsubscribe(Action<GameEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _allHandlers.Remove(handler);
    }

    /// <summary>
    /// 发布事件：先派发给精确类型订阅者，再派发给全局订阅者。
    /// <paramref name="gameEvent"/> 为 null 时抛 <see cref="ArgumentNullException"/>；无订阅者时静默返回。
    /// </summary>
    public void Publish<T>(T gameEvent) where T : GameEvent
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        if (_typedHandlers.TryGetValue(gameEvent.GetType(), out var list))
        {
            foreach (var subscription in list.ToArray())
                InvokeSafely(subscription.Invoke, gameEvent);
        }

        if (_allHandlers.Count == 0)
            return;

        foreach (var handler in _allHandlers.ToArray())
            InvokeSafely(handler, gameEvent);
    }

    /// <summary>
    /// 隔离单个订阅者异常：契约上「订阅者异常不应污染核心流程」——
    /// 一个订阅者抛异常不得打断其余订阅者，也不得中断发布该事件的核心动作，
    /// 故此处静默吞掉。Core 不引入 Godot / 日志，无副作用可做。
    /// </summary>
    private static void InvokeSafely(Action<GameEvent> handler, GameEvent gameEvent)
    {
        try
        {
            handler(gameEvent);
        }
        catch
        {
            // 订阅者异常不应污染核心流程。
        }
    }
}
