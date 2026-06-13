namespace Logis.Services;

/// <summary>
/// Defines the type of change for a specific line in a diff.
/// </summary>
public enum DiffOp { Unchanged, Inserted, Deleted }

/// <summary>
/// Represents a single line in a diff result.
/// </summary>
public record DiffLine(DiffOp Op, string Text);

/// <summary>
/// Provides a line-level Longest Common Subsequence (LCS) differ for comparing text.
/// </summary>
public class DiffEngine
{
    /// <summary>
    /// Computes the optimal set of changes to transform array 'a' into array 'b'.
    /// </summary>
    public List<DiffLine> ComputeDiff(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        int[,] d = new int[n + 1, m + 1];

        // Fill LCS matrix
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                d[i, j] = (a[i - 1] == b[j - 1]) ? d[i - 1, j - 1] + 1 : Math.Max(d[i - 1, j], d[i, j - 1]);
            }
        }

        // Backtrack to find the diff
        var result = new List<DiffLine>();
        int x = n, y = m;
        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && a[x - 1] == b[y - 1])
            {
                result.Add(new DiffLine(DiffOp.Unchanged, a[x - 1]));
                x--; y--;
            }
            else if (y > 0 && (x == 0 || d[x, y - 1] >= d[x - 1, y]))
            {
                result.Add(new DiffLine(DiffOp.Inserted, b[y - 1]));
                y--;
            }
            else
            {
                result.Add(new DiffLine(DiffOp.Deleted, a[x - 1]));
                x--;
            }
        }
        
        result.Reverse();
        return result;
    }
}
