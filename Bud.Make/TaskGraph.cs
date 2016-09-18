using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Bud.Make {
  /// <summary>
  ///   A wrapper around <see cref="Task" />
  /// </summary>
  public class TaskGraph {
    /// <summary>
    ///   The name of this task.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   The action that this task will execute.
    /// </summary>
    public Action Action { get; }

    /// <summary>
    ///   The task on which this task depends. These will be executed before this task.
    /// </summary>
    public ImmutableArray<TaskGraph> Dependencies { get; }

    internal TaskGraph(string name, Action action, ImmutableArray<TaskGraph> dependencies) {
      Name = name;
      Action = action;
      Dependencies = dependencies;
    }

    internal TaskGraph(ImmutableArray<TaskGraph> subGraphs) : this(null, null, subGraphs) {}

    /// <summary>
    ///   Executes all tasks in this graph in parallel (using the <see cref="Task" /> API).
    ///   This method blocks.
    /// </summary>
    public void Run() => RunAsync().Wait();

    /// <summary>
    ///   Asynchronously executes all tasks in this graph in parallel (using the <see cref="Task" /> API).
    /// </summary>
    public async Task RunAsync() => await ToTask(new Dictionary<string, Task>());

    /// <summary>
    ///   Converts objects of type <typeparamref name="T" /> into a task graph. Each object must have a name. The name is
    ///   retrieved via the given <paramref name="nameOfTask" /> callback function. Tasks with the same name are considered
    ///   identical. Dependencies of an object are retrieved via the <paramref name="dependenciesOfTask" /> function. If
    ///   multiple tasks depend on tasks with the same, then they will share the tasks graph node and its action will be
    ///   invoked only once. The action of a task is retrieved via the <paramref name="actionOfTask" /> function.
    /// </summary>
    /// <typeparam name="T">the type of objects to convert to a task graph.</typeparam>
    /// <param name="rootTasks">the task objects from which to start building the task graph.</param>
    /// <param name="nameOfTask">the function that returns the name of the given task object.</param>
    /// <param name="dependenciesOfTask">the function that returns task objects on which the given task object depends.</param>
    /// <param name="actionOfTask">the function that returns the action for the given task object.</param>
    /// <returns>a task graph that can be executed.</returns>
    public static TaskGraph ToTaskGraph<T>(IEnumerable<T> rootTasks, Func<T, string> nameOfTask, Func<T, IEnumerable<T>> dependenciesOfTask, Func<T, Action> actionOfTask) {
      var visitedTasks = new Dictionary<string, TaskGraph>();
      var dependentsOfThisTask = new HashSet<string>();
      var orderedDependents = new List<string>();
      var taskGraphs = ImmutableArray.CreateBuilder<TaskGraph>();
      foreach (var task in rootTasks) {
        var ruleTaskGraph = BuildTaskGraph(task, nameOfTask, dependenciesOfTask, actionOfTask, visitedTasks, dependentsOfThisTask, orderedDependents);
        taskGraphs.Add(ruleTaskGraph);
      }
      return new TaskGraph(taskGraphs.ToImmutable());
    }

    private static TaskGraph BuildTaskGraph<T>(T task, Func<T, string> nameOfTask, Func<T, IEnumerable<T>> dependenciesOfTask, Func<T, Action> actionOfTask, IDictionary<string, TaskGraph> visitedTasks, HashSet<string> dependentsOfThisTask, List<string> orderedDependents) {
      var taskName = nameOfTask(task);
      if (dependentsOfThisTask.Contains(taskName)) {
        throw new Exception($"Detected a cycle in dependencies: '{string.Join(" depends on ", orderedDependents)} depends on {taskName}'.");
      }
      TaskGraph cachedTaskGraph;
      if (visitedTasks.TryGetValue(taskName, out cachedTaskGraph)) {
        return cachedTaskGraph;
      }
      dependentsOfThisTask.Add(taskName);
      orderedDependents.Add(taskName);
      var dependencyTasks = ImmutableArray.CreateBuilder<TaskGraph>();
      foreach (var dependencyTask in dependenciesOfTask(task)) {
        var dependencyTaskGraph = BuildTaskGraph(dependencyTask, nameOfTask, dependenciesOfTask, actionOfTask, visitedTasks, dependentsOfThisTask, orderedDependents);
        dependencyTasks.Add(dependencyTaskGraph);
      }
      var thisTaskGraph = new TaskGraph(taskName, actionOfTask(task), dependencyTasks.ToImmutable());
      visitedTasks.Add(taskName, thisTaskGraph);
      dependentsOfThisTask.Remove(taskName);
      orderedDependents.RemoveAt(orderedDependents.Count - 1);
      return thisTaskGraph;
    }

    private Task ToTask(IDictionary<string, Task> existingTasks) {
      Task task;
      if (Name != null && existingTasks.TryGetValue(Name, out task)) {
        return task;
      }
      if (Dependencies.Length <= 0) {
        task = Action == null ? Task.CompletedTask : Task.Factory.StartNew(Action);
      } else {
        task = Task.WhenAll(Dependencies.Select(tg => tg.ToTask(existingTasks)));
        task = Action == null ? task : task.ContinueWith(t => Action());
      }
      if (Name != null) {
        existingTasks.Add(Name, task);
      }
      return task;
    }
  }
}