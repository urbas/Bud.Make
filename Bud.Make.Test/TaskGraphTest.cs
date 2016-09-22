using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Moq;
using NUnit.Framework;

namespace Bud.Make {
  public class TaskGraphTest {
    [Test]
    public void Run_invokes_action() {
      var action = new Mock<Action>();
      new TaskGraph(null, action.Object, ImmutableArray<TaskGraph>.Empty).Run();
      action.Verify(a => a(), Times.Once);
    }

    [Test]
    public void Run_invokes_actions_of_dependencies_before_dependent() {
      var queue = new ConcurrentQueue<int>();
      var dependency = new TaskGraph(null, () => queue.Enqueue(1), ImmutableArray<TaskGraph>.Empty);
      var graph = new TaskGraph(null, () => queue.Enqueue(2), ImmutableArray.Create(dependency));
      graph.Run();
      Assert.AreEqual(new[] {1, 2}, queue);
    }

    [Test]
    public void Run_invokes_dependencies_with_same_name_once() {
      var action = new Mock<Action>();
      var dependency = new TaskGraph("foo", action.Object, ImmutableArray<TaskGraph>.Empty);
      var graph = new TaskGraph(null, () => {}, ImmutableArray.Create(dependency, dependency));
      graph.Run();
      action.Verify(a => a(), Times.Once);
    }

    [Test]
    public void ToTaskGraph_creates_correctly_shaped_graph() {
      var deps = ImmutableDictionary<string, IEnumerable<string>>
        .Empty
        .Add("a", new[] {"b", "c"})
        .Add("b", new[] {"d"})
        .Add("c", new[] {"d"})
        .Add("d", Array.Empty<string>());

      var actions = ImmutableDictionary<string, Action>
        .Empty
        .Add("a", new Mock<Action>().Object)
        .Add("b", new Mock<Action>().Object)
        .Add("c", new Mock<Action>().Object)
        .Add("d", new Mock<Action>().Object);

      var taskGraph = TaskGraph.ToTaskGraph(new[] {"a", "b"}, s => s, s => deps[s], s => actions[s]);

      var a = taskGraph.Dependencies[0];
      var b = taskGraph.Dependencies[1];
      var c = a.Dependencies[1];
      var d = b.Dependencies[0];

      Assert.IsNull(taskGraph.Name);
      Assert.AreEqual("a", a.Name);
      Assert.AreEqual("b", b.Name);
      Assert.AreEqual("c", c.Name);
      Assert.AreEqual("d", d.Name);

      Assert.AreEqual(new[] {a, b}, taskGraph.Dependencies);
      Assert.AreEqual(new[] {b, c}, a.Dependencies);
      Assert.AreEqual(new[] {d}, b.Dependencies);
      Assert.AreEqual(new[] {d}, c.Dependencies);
      Assert.IsEmpty(d.Dependencies);

      Assert.IsNull(taskGraph.Action);
      Assert.AreSame(actions["a"], a.Action);
      Assert.AreSame(actions["b"], b.Action);
      Assert.AreSame(actions["c"], c.Action);
      Assert.AreSame(actions["d"], d.Action);
    }

    [Test]
    public void ToTaskGraph_throws_when_cycles_present() {
      var deps = ImmutableDictionary<string, IEnumerable<string>>
        .Empty
        .Add("a", new[] {"b"})
        .Add("b", new[] {"c"})
        .Add("c", new[] {"a"});

      var ex = Assert.Throws<Exception>(() => TaskGraph.ToTaskGraph(new[] {"a"}, s => s, s => deps[s], s => null));

      Assert.That(ex.Message,
                  Does.Contain("a depends on b depends on c depends on a"));
    }
  }
}