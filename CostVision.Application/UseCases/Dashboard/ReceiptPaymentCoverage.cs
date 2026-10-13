namespace CostVision.Application.UseCases.Dashboard;

// Максимальное покрытие по существующим связям: рубль платежа не покрывает два документа.
// Обратные рёбра позволяют перераспределить покрытие, сохранив приоритет более ранних документов.
internal static class ReceiptPaymentCoverage
{
    public static decimal[] CalculateRemaining(IReadOnlyList<ReportReceipt> receipts, IReadOnlyList<ReportMoneyMovement> movements)
    {
        int sink = receipts.Count + movements.Count + 1;
        List<Edge>[] graph = Enumerable.Range(0, sink + 1).Select(_ => new List<Edge>()).ToArray();
        Dictionary<Guid, int> movementNodes = new();
        for (int index = 0; index < movements.Count; index++)
        {
            int node = receipts.Count + index + 1;
            movementNodes.Add(movements[index].Id, node);
            AddEdge(graph, node, sink, movements[index].Amount);
        }

        Edge[] receiptEdges = new Edge[receipts.Count];
        for (int index = 0; index < receipts.Count; index++)
        {
            int node = index + 1;
            receiptEdges[index] = AddEdge(graph, 0, node, 0m);
            foreach (Guid id in receipts[index].MovementIds.Distinct())
            {
                if (movementNodes.TryGetValue(id, out int movementNode))
                {
                    AddEdge(graph, node, movementNode, receipts[index].Identity.TotalSum);
                }
            }
        }

        // Включаем документы последовательно; уже покрытая сумма ранних документов не уменьшается.
        for (int index = 0; index < receipts.Count; index++)
        {
            receiptEdges[index].Capacity = receipts[index].Identity.TotalSum;
            while (TryAugment(graph, sink))
            {
            }
        }

        return receiptEdges.Select(edge => edge.Capacity).ToArray();
    }

    private static Edge AddEdge(List<Edge>[] graph, int from, int to, decimal capacity)
    {
        Edge forward = new(to, graph[to].Count, capacity);
        Edge reverse = new(from, graph[from].Count, 0m);
        graph[from].Add(forward);
        graph[to].Add(reverse);
        return forward;
    }

    private static bool TryAugment(List<Edge>[] graph, int sink)
    {
        int[] previousNodes = Enumerable.Repeat(-1, graph.Length).ToArray();
        int[] previousEdges = new int[graph.Length];
        Queue<int> queue = new();
        previousNodes[0] = 0;
        queue.Enqueue(0);
        while (queue.Count > 0 && previousNodes[sink] == -1)
        {
            int node = queue.Dequeue();
            for (int index = 0; index < graph[node].Count; index++)
            {
                Edge edge = graph[node][index];
                if (edge.Capacity <= 0m || previousNodes[edge.To] != -1)
                {
                    continue;
                }

                previousNodes[edge.To] = node;
                previousEdges[edge.To] = index;
                queue.Enqueue(edge.To);
            }
        }

        if (previousNodes[sink] == -1)
        {
            return false;
        }

        decimal amount = decimal.MaxValue;
        for (int node = sink; node != 0; node = previousNodes[node])
        {
            Edge edge = graph[previousNodes[node]][previousEdges[node]];
            amount = Math.Min(amount, edge.Capacity);
        }

        for (int node = sink; node != 0; node = previousNodes[node])
        {
            Edge edge = graph[previousNodes[node]][previousEdges[node]];
            edge.Capacity -= amount;
            graph[node][edge.ReverseIndex].Capacity += amount;
        }

        return true;
    }

    private sealed class Edge(int to, int reverseIndex, decimal capacity)
    {
        public int To { get; } = to;
        public int ReverseIndex { get; } = reverseIndex;
        public decimal Capacity { get; set; } = capacity;
    }
}
