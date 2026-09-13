using SevenSpices.Core.Events;

namespace SevenSpices.Tests.Events;

/// <summary>
/// EventBus 单元测试：精确类型订阅、全局订阅、取消订阅、多订阅者、
/// 无订阅者不抛、精确运行时类型匹配、实例独立、派发顺序、
/// 订阅者异常隔离、派发中增删订阅。
/// </summary>
public static class EventBusTests
{
    private class BaseDummyEvent : GameEvent
    {
    }

    private sealed class ChildDummyEvent : BaseDummyEvent
    {
    }

    private sealed class OtherDummyEvent : GameEvent
    {
    }

    public static void RunAll()
    {
        Test_ExactType_OnlyMatchingSubscriber();
        Test_AllEventSubscription_ReceivesEveryType();
        Test_MultipleSubscribers_AllInvoked();
        Test_Unsubscribe_Typed();
        Test_Unsubscribe_All();
        Test_Publish_NoSubscribers_DoesNotThrow();
        Test_Publish_TypedBeforeAll();
        Test_ExactRuntimeType_NotBaseType();
        Test_Subscriptions_ArePerInstance();
        Test_SubscribeOrPublish_Null_Throws();
        Test_SubscriberException_DoesNotBreakOthersOrPropagate();
        Test_GlobalSubscriberException_DoesNotBreakOthers();
        Test_UnsubscribeSelf_DuringDispatch_SnapshotThenGone();
        Test_UnsubscribeOther_DuringDispatch_SnapshotThenGone();
        Test_Subscribe_DuringDispatch_OnlyNextPublish();

        Console.WriteLine("All EventBusTests passed.");
    }

    static void Test_ExactType_OnlyMatchingSubscriber()
    {
        var bus = new EventBus();
        int count = 0;
        bus.Subscribe<ChildDummyEvent>(_ => count++);

        bus.Publish(new OtherDummyEvent());
        Assert(count == 0, "非匹配类型不应触发精确订阅");

        bus.Publish(new ChildDummyEvent());
        Assert(count == 1, "匹配类型应触发一次");
    }

    static void Test_AllEventSubscription_ReceivesEveryType()
    {
        var bus = new EventBus();
        var received = new List<GameEvent>();
        bus.Subscribe(received.Add);

        bus.Publish(new ChildDummyEvent());
        bus.Publish(new OtherDummyEvent());

        Assert(received.Count == 2, $"全局订阅应收到全部 2 个事件，实际 {received.Count}");
        Assert(received[0] is ChildDummyEvent, "第 1 个事件类型应为 ChildDummyEvent");
        Assert(received[1] is OtherDummyEvent, "第 2 个事件类型应为 OtherDummyEvent");
    }

    static void Test_MultipleSubscribers_AllInvoked()
    {
        var bus = new EventBus();
        int a = 0, b = 0, all = 0;
        bus.Subscribe<ChildDummyEvent>(_ => a++);
        bus.Subscribe<ChildDummyEvent>(_ => b++);
        bus.Subscribe(_ => all++);

        bus.Publish(new ChildDummyEvent());

        Assert(a == 1 && b == 1, "同类型多个订阅者都应被调用");
        Assert(all == 1, "全局订阅者也应被调用");
    }

    static void Test_Unsubscribe_Typed()
    {
        var bus = new EventBus();
        int count = 0;
        Action<ChildDummyEvent> handler = _ => count++;

        bus.Subscribe(handler);
        bus.Publish(new ChildDummyEvent());
        Assert(count == 1, "退订前应收到事件");

        bus.Unsubscribe(handler);
        bus.Publish(new ChildDummyEvent());
        Assert(count == 1, "退订后不应再收到事件");
    }

    static void Test_Unsubscribe_All()
    {
        var bus = new EventBus();
        int count = 0;
        Action<GameEvent> handler = _ => count++;

        bus.Subscribe(handler);
        bus.Publish(new OtherDummyEvent());
        Assert(count == 1, "退订前全局订阅应收到事件");

        bus.Unsubscribe(handler);
        bus.Publish(new OtherDummyEvent());
        Assert(count == 1, "退订后全局订阅不应再收到事件");
    }

    static void Test_Publish_NoSubscribers_DoesNotThrow()
    {
        var bus = new EventBus();
        bool threw = false;
        try { bus.Publish(new ChildDummyEvent()); }
        catch { threw = true; }
        Assert(!threw, "无订阅者时 Publish 不应抛异常");
    }

    static void Test_Publish_TypedBeforeAll()
    {
        var bus = new EventBus();
        var order = new List<string>();
        bus.Subscribe<ChildDummyEvent>(_ => order.Add("typed"));
        bus.Subscribe(_ => order.Add("all"));

        bus.Publish(new ChildDummyEvent());

        Assert(order.Count == 2 && order[0] == "typed" && order[1] == "all",
            "应先触发精确类型订阅者，再触发全局订阅者");
    }

    static void Test_ExactRuntimeType_NotBaseType()
    {
        var bus = new EventBus();
        int baseCount = 0;
        int allCount = 0;
        bus.Subscribe<BaseDummyEvent>(_ => baseCount++);
        bus.Subscribe(_ => allCount++);

        bus.Publish(new ChildDummyEvent());

        Assert(baseCount == 0, "泛型订阅按运行时精确类型匹配，基类订阅不应收到子类事件");
        Assert(allCount == 1, "全局订阅仍应收到子类事件");
    }

    static void Test_Subscriptions_ArePerInstance()
    {
        var first = new EventBus();
        var second = new EventBus();
        int firstCount = 0, secondCount = 0;
        first.Subscribe(_ => firstCount++);
        second.Subscribe(_ => secondCount++);

        first.Publish(new ChildDummyEvent());

        Assert(firstCount == 1 && secondCount == 0, "事件总线实例之间不应共享订阅");
    }

    static void Test_SubscribeOrPublish_Null_Throws()
    {
        var bus = new EventBus();
        AssertThrows<ArgumentNullException>(() => bus.Subscribe<ChildDummyEvent>(null!),
            "Subscribe<T> null 应抛 ArgumentNullException");
        AssertThrows<ArgumentNullException>(() => bus.Subscribe((Action<GameEvent>)null!),
            "Subscribe(Action<GameEvent>) null 应抛 ArgumentNullException");
        AssertThrows<ArgumentNullException>(() => bus.Publish<ChildDummyEvent>(null!),
            "Publish null 应抛 ArgumentNullException");
    }

    /// <summary>
    /// 单个订阅者抛异常时：其余订阅者仍被调用，且 Publish 不向外抛异常。
    /// </summary>
    static void Test_SubscriberException_DoesNotBreakOthersOrPropagate()
    {
        var bus = new EventBus();
        int beforeCount = 0, afterCount = 0, allCount = 0;
        bus.Subscribe<ChildDummyEvent>(_ => beforeCount++);
        bus.Subscribe<ChildDummyEvent>(_ => throw new InvalidOperationException("typed boom"));
        bus.Subscribe<ChildDummyEvent>(_ => afterCount++);
        bus.Subscribe(_ => allCount++);

        bool threw = false;
        try { bus.Publish(new ChildDummyEvent()); }
        catch { threw = true; }

        Assert(!threw, "单个订阅者抛异常不应使 Publish 向外抛出");
        Assert(beforeCount == 1 && afterCount == 1, "异常订阅者前后的精确类型订阅者都应被调用");
        Assert(allCount == 1, "异常不应阻止后续全局订阅者被调用");
    }

    /// <summary>
    /// 全局订阅者抛异常时：其后的全局订阅者仍被调用，Publish 不向外抛异常。
    /// </summary>
    static void Test_GlobalSubscriberException_DoesNotBreakOthers()
    {
        var bus = new EventBus();
        int first = 0, third = 0;
        bus.Subscribe(_ => first++);
        bus.Subscribe(_ => throw new InvalidOperationException("global boom"));
        bus.Subscribe(_ => third++);

        bool threw = false;
        try { bus.Publish(new ChildDummyEvent()); }
        catch { threw = true; }

        Assert(!threw, "全局订阅者抛异常不应使 Publish 向外抛出");
        Assert(first == 1 && third == 1, "全局订阅者异常前后的其余订阅者都应被调用");
    }

    /// <summary>handler 内退订自身：本次仍按快照调用，下次不再调用。</summary>
    static void Test_UnsubscribeSelf_DuringDispatch_SnapshotThenGone()
    {
        var bus = new EventBus();
        int selfCount = 0, otherCount = 0;
        Action<ChildDummyEvent>? self = null;
        self = _ =>
        {
            selfCount++;
            bus.Unsubscribe(self!);
        };
        bus.Subscribe(self!);
        bus.Subscribe<ChildDummyEvent>(_ => otherCount++);

        bus.Publish(new ChildDummyEvent());
        Assert(selfCount == 1 && otherCount == 1, "派发中退订自身：本次快照仍应调用该订阅者");

        bus.Publish(new ChildDummyEvent());
        Assert(selfCount == 1 && otherCount == 2, "退订自身后下次派发不应再调用该订阅者");
    }

    /// <summary>handler 内退订其他 handler：本次仍按快照调用，下次不再调用。</summary>
    static void Test_UnsubscribeOther_DuringDispatch_SnapshotThenGone()
    {
        var bus = new EventBus();
        int firstCount = 0, secondCount = 0;
        Action<ChildDummyEvent>? second = null;
        Action<ChildDummyEvent> first = _ =>
        {
            firstCount++;
            bus.Unsubscribe(second!);
        };
        second = _ => secondCount++;

        bus.Subscribe(first);
        bus.Subscribe(second!);

        bus.Publish(new ChildDummyEvent());
        Assert(firstCount == 1 && secondCount == 1, "派发中退订其他 handler：本次快照仍应调用两者");

        bus.Publish(new ChildDummyEvent());
        Assert(firstCount == 2 && secondCount == 1, "被退订的 handler 下次派发不应再被调用");
    }

    /// <summary>handler 内新增订阅者：本次派发不调用，下次调用。</summary>
    static void Test_Subscribe_DuringDispatch_OnlyNextPublish()
    {
        var bus = new EventBus();
        int addedCount = 0;
        bool added = false;
        Action<ChildDummyEvent> addedHandler = _ => addedCount++;

        bus.Subscribe<ChildDummyEvent>(_ =>
        {
            if (added) return;
            added = true;
            bus.Subscribe(addedHandler);
        });

        bus.Publish(new ChildDummyEvent());
        Assert(addedCount == 0, "派发中新增订阅者不应参与本次派发");

        bus.Publish(new ChildDummyEvent());
        Assert(addedCount == 1, "下次派发应调用新增订阅者");
    }

    static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        bool threw = false;
        try { action(); }
        catch (TException) { threw = true; }
        Assert(threw, message);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[FAIL] EventBusTests: {message}");
    }
}
