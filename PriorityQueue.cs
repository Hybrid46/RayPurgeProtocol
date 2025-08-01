public class PriorityQueue<T>
{
    private List<(float Priority, T Item)> _heap = new List<(float, T)>();
    private Dictionary<T, int> _itemIndices = new Dictionary<T, int>();

    public int Count => _heap.Count;

    public void Enqueue(T item, float priority)
    {
        _heap.Add((priority, item));
        int childIndex = _heap.Count - 1;
        _itemIndices[item] = childIndex;  // Update index tracking

        // Heapify-up
        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2;
            if (_heap[parentIndex].Priority <= _heap[childIndex].Priority)
                break;

            Swap(parentIndex, childIndex);
            childIndex = parentIndex;
        }
    }

    public T Dequeue()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Queue is empty");

        T result = _heap[0].Item;
        RemoveIndex(result);  // Remove index tracking

        int lastIdx = _heap.Count - 1;
        _heap[0] = _heap[lastIdx];
        _heap.RemoveAt(lastIdx);

        if (_heap.Count > 0)
        {
            UpdateIndex(0);  // Update index tracking
            HeapifyDown(0);
        }

        return result;
    }

    public bool TryDequeue(out T item, out float priority)
    {
        if (Count == 0)
        {
            item = default;
            priority = default;
            return false;
        }

        priority = _heap[0].Priority;
        item = Dequeue();
        return true;
    }

    public void UpdatePriority(T item, float newPriority)
    {
        if (!_itemIndices.TryGetValue(item, out int index))
            throw new ArgumentException("Item not in queue");

        float oldPriority = _heap[index].Priority;
        _heap[index] = (newPriority, item);

        if (newPriority < oldPriority)
            HeapifyUp(index);
        else
            HeapifyDown(index);
    }

    public bool Contains(T item) => _itemIndices.ContainsKey(item);

    public void Clear()
    {
        _heap.Clear();
        _itemIndices.Clear();
    }

    private void HeapifyUp(int childIndex)
    {
        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2;
            if (_heap[parentIndex].Priority <= _heap[childIndex].Priority)
                break;

            Swap(parentIndex, childIndex);
            childIndex = parentIndex;
        }
    }

    private void HeapifyDown(int parentIndex)
    {
        while (true)
        {
            int leftChild = 2 * parentIndex + 1;
            int rightChild = 2 * parentIndex + 2;
            int smallest = parentIndex;

            if (leftChild < _heap.Count && _heap[leftChild].Priority < _heap[smallest].Priority)
                smallest = leftChild;

            if (rightChild < _heap.Count && _heap[rightChild].Priority < _heap[smallest].Priority)
                smallest = rightChild;

            if (smallest == parentIndex) break;

            Swap(parentIndex, smallest);
            parentIndex = smallest;
        }
    }

    private void Swap(int indexA, int indexB)
    {
        (_heap[indexA], _heap[indexB]) = (_heap[indexB], _heap[indexA]);
        UpdateIndex(indexA);
        UpdateIndex(indexB);
    }

    // Index tracking helpers
    private void UpdateIndex(int index) => _itemIndices[_heap[index].Item] = index;
    private void RemoveIndex(T item) => _itemIndices.Remove(item);
}