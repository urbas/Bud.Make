using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace Bud.Make {
  public class TaskGraphTest {
    [Test]
    public void Run_invokes_action_of_single_anonymous_task() {
      var action = new Mock<Action>();
      new TaskGraph(null, action.Object, ImmutableArray<TaskGraph>.Empty).Run();
      action.Verify(a => a(), Times.Once);
    }

    [Test]
    public void Run_invokes_actions_of_dependencies_before_dependent() {
      var latch = new CountdownEvent(1);
      new TaskGraph(null, () => latch.Wait(),
                    ImmutableArray.Create(new TaskGraph(null, () => latch.Signal(), ImmutableArray<TaskGraph>.Empty)))
        .Run();
    }

    [Test]
    public void Run_invokes_dependencies_with_same_name_once() {
      var action = new Mock<Action>();
      var dependency = new TaskGraph("foo", action.Object, ImmutableArray<TaskGraph>.Empty);
      new TaskGraph(null, () => {}, ImmutableArray.Create(dependency, dependency)).Run();
      action.Verify(a => a(), Times.Once);
    }

    [Test]
    public void ToTaskGraph_single_task() {
      var deps = ImmutableDictionary<string, IEnumerable<string>>
        .Empty.Add("a", new[] {"b", "c"}).Add("b", new[] {"d"}).Add("c", new[] {"d"});
      var actions = ImmutableDictionary<string, Mock<Action>>
        .Empty.Add("a", new Mock<Action>()).Add("b", new Mock<Action>()).Add("c", new Mock<Action>())
        .Add("d", new Mock<Action>());
      var taskGraph = TaskGraph.ToTaskGraph(new[] {"a", "b"},
                                            s => s,
                                            s => deps.Get(s).GetOrElse(Enumerable.Empty<string>()),
                                            s => actions[s].Object);
      var a = taskGraph.Dependencies[0];
      var b = taskGraph.Dependencies[1];
      var c = a.Dependencies[1];
      var d = b.Dependencies[0];

      Assert.IsNull(taskGraph.Name);
      Assert.AreSame("a", a.Name);
      Assert.AreSame("b", b.Name);
      Assert.AreSame("c", c.Name);
      Assert.AreSame("d", d.Name);

      Assert.AreEqual(new[] {a, b}, taskGraph.Dependencies);
      Assert.AreEqual(new[] {b, c}, a.Dependencies);
      Assert.AreEqual(new[] {d}, b.Dependencies);
      Assert.AreEqual(new[] {d}, c.Dependencies);
      Assert.IsEmpty(d.Dependencies);

      Assert.IsNull(taskGraph.Action);
      Assert.AreSame(actions["a"].Object, a.Action);
      Assert.AreSame(actions["b"].Object, b.Action);
      Assert.AreSame(actions["c"].Object, c.Action);
      Assert.AreSame(actions["d"].Object, d.Action);
    }

    [Test]
    public void ToTaskGraph_throws_when_cycles_present() {
      var deps = ImmutableDictionary<string, IEnumerable<string>>
        .Empty.Add("a", new[] {"b"}).Add("b", new[] {"c"}).Add("c", new[] {"a"});
      var ex = Assert.Throws<Exception>(() => TaskGraph.ToTaskGraph(new[] {"a"}, s => s, s => deps[s], s => null));
      Assert.That(ex.Message, Does.Contain("a depends on b depends on c depends on a"));
    }
  }
}