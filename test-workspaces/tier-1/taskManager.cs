using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace TaskManager
{
    public class TaskItem
    {
        public int Id;
        public string Title;
        public string Description;
        public bool IsComplete;
        public DateTime CreatedAt;
        public string Priority;

        public TaskItem(int id, string title, string description, string priority)
        {
            Id = id;
            Title = title;
            Description = description;
            IsComplete = false;
            CreatedAt = DateTime.Now;
            Priority = priority;
        }
    }

    public class TaskManager
    {
        private ArrayList _tasks = new ArrayList();
        private int _nextId = 1;

        public void AddTask(string title, string description, string priority)
        {
            var task = new TaskItem(_nextId, title, description, priority);
            _tasks.Add(task);
            _nextId++;
        }

        public void CompleteTask(int id)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem task = (TaskItem)_tasks[i];
                if (task.Id == id)
                {
                    task.IsComplete = true;
                    return;
                }
            }
            Console.WriteLine("Task not found: " + id);
        }

        public void RemoveTask(int id)
        {
            TaskItem toRemove = null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem task = (TaskItem)_tasks[i];
                if (task.Id == id)
                {
                    toRemove = task;
                    break;
                }
            }
            if (toRemove != null)
            {
                _tasks.Remove(toRemove);
            }
            else
            {
                Console.WriteLine("Task not found: " + id);
            }
        }

        public ArrayList GetPendingTasks()
        {
            ArrayList pending = new ArrayList();
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem task = (TaskItem)_tasks[i];
                if (task.IsComplete == false)
                {
                    pending.Add(task);
                }
            }
            return pending;
        }

        public ArrayList GetTasksByPriority(string priority)
        {
            ArrayList result = new ArrayList();
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem task = (TaskItem)_tasks[i];
                if (task.Priority == priority)
                {
                    result.Add(task);
                }
            }
            return result;
        }

        public string GetSummary(out int total, out int completed, out int pending)
        {
            total = _tasks.Count;
            completed = 0;
            pending = 0;

            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem task = (TaskItem)_tasks[i];
                if (task.IsComplete)
                {
                    completed++;
                }
                else
                {
                    pending++;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("Task Summary: ");
            sb.Append("Total=" + total);
            sb.Append(", Completed=" + completed);
            sb.Append(", Pending=" + pending);
            return sb.ToString();
        }

        public void PrintAllTasks()
        {
            if (_tasks.Count == 0)
            {
                Console.WriteLine("No tasks.");
                return;
            }

            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskItem task = (TaskItem)_tasks[i];
                string status = task.IsComplete ? "DONE" : "PENDING";
                Console.WriteLine("[" + task.Id + "] " + task.Title + " (" + task.Priority + ") - " + status);
                if (task.Description != null && task.Description != "")
                {
                    Console.WriteLine("    " + task.Description);
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var manager = new TaskManager();

            manager.AddTask("Buy groceries", "Milk, eggs, bread", "low");
            manager.AddTask("Fix login bug", "Users report 401 on token refresh", "high");
            manager.AddTask("Write unit tests", "Coverage for auth module", "medium");
            manager.AddTask("Deploy to staging", "After tests pass", "high");
            manager.AddTask("Update documentation", "API docs are out of date", "low");

            manager.CompleteTask(1);
            manager.CompleteTask(3);

            manager.PrintAllTasks();

            Console.WriteLine();

            int total, completed, pending;
            string summary = manager.GetSummary(out total, out completed, out pending);
            Console.WriteLine(summary);

            Console.WriteLine();
            Console.WriteLine("High priority tasks:");

            ArrayList highPriority = manager.GetTasksByPriority("high");
            for (int i = 0; i < highPriority.Count; i++)
            {
                TaskItem task = (TaskItem)highPriority[i];
                Console.WriteLine("  - " + task.Title);
            }
        }
    }
}