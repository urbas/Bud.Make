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
    ///   The task on which this task depends. These will be executed before this task.
    /// </summary>
    public ImmutableArray<TaskGraph> Dependencies { get; }

    /// <summary>
    ///   The action that this task will execute.
    /// </summary>
    public Action Action { get; }

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
    public static TaskGraph ToTaskGraph<T>(IEnumerable<T> rootTasks,
                                           Func<T, string> nameOfTask,
                                           Func<T, IEnumerable<T>> dependenciesOfTask,
                                           Func<T, Action> actionOfTask)
      => new TaskGraphBuilder<T>(nameOfTask, dependenciesOfTask, actionOfTask).ToTaskGraph(rootTasks);

    private Task ToTask(IDictionary<string, Task> existingTasks) {
      Task task;
      if (Name != null && existingTasks.TryGetValue(Name, out task)) {
        return task;
      }
      if (Dependencies.Length <= 0) {
        task = Action == null ? Task.CompletedTask : Task.Run(Action);
      } else {
        task = Task.WhenAll(Dependencies.Select(tg => tg.ToTask(existingTasks)));
        task = Action == null ? task : task.ContinueWith(t => Action());
      }
      if (Name != null) {
        existingTasks.Add(Name, task);
      }
      return task;
    }

    private class TaskGraphBuilder<TTask> {
      private readonly Func<TTask, IEnumerable<TTask>> dependenciesOfTask;

      public TaskGraphBuilder(Func<TTask, string> nameOfTask,
                              Func<TTask, IEnumerable<TTask>> dependenciesOfTask,
                              Func<TTask, Action> actionOfTask) {
        this.dependenciesOfTask = dependenciesOfTask;
        ActionOfTask = actionOfTask;
        NameOfTask = nameOfTask;
        FinishedTasks = new Dictionary<string, TaskGraph>();
        DependencyChain = new HashSet<string>();
        OrderedDependencyChain = new List<string>();
      }

      private Func<TTask, Action> ActionOfTask { get; }
      private Func<TTask, string> NameOfTask { get; }
      private IDictionary<string, TaskGraph> FinishedTasks { get; }
      private HashSet<string> DependencyChain { get; }
      private List<string> OrderedDependencyChain { get; }

      public TaskGraph ToTaskGraph(IEnumerable<TTask> tasks) => new TaskGraph(ToTaskGraphs(tasks));

      private ImmutableArray<TaskGraph> ToTaskGraphs(IEnumerable<TTask> tasks)
        => tasks.Select(ToTaskGraph).ToImmutableArray();

      private TaskGraph ToTaskGraph(TTask task) {
        var taskName = NameOfTask(task);
        TaskGraph cachedTaskGraph;
        if (FinishedTasks.TryGetValue(taskName, out cachedTaskGraph)) {
          return cachedTaskGraph;
        }
        DescendOneLevel(taskName);
        var dependencyTasks = ToTaskGraphs(dependenciesOfTask(task));
        AscendOneLevel(taskName);
        return CreateTaskGraph(task, taskName, dependencyTasks);
      }

      private void DescendOneLevel(string taskName) {
        if (DependencyChain.Contains(taskName)) {
          throw new Exception("Detected a dependency cycle: " +
                              $"'{string.Join(" depends on ", OrderedDependencyChain)} " +
                              $"depends on {taskName}'.");
        }
        DependencyChain.Add(taskName);
        OrderedDependencyChain.Add(taskName);
      }

      private void AscendOneLevel(string taskName) {
        DependencyChain.Remove(taskName);
        OrderedDependencyChain.RemoveAt(OrderedDependencyChain.Count - 1);
      }

      private TaskGraph CreateTaskGraph(TTask task, string taskName, ImmutableArray<TaskGraph> dependencyTasks) {
        var thisTaskGraph = new TaskGraph(taskName, ActionOfTask(task), dependencyTasks);
        FinishedTasks.Add(taskName, thisTaskGraph);
        return thisTaskGraph;
      }
    }
  }
}