using System;
using UnityEngine;

namespace uconsole
{
    public class CircularBuffer<T>
    {
        private readonly T[] _array;
        private int _startIndex;

        public int Count { get; private set; }
        public T this[int index] => _array[(_startIndex + index) % _array.Length];

        public CircularBuffer(int capacity)
        {
            _array = new T[capacity];
        }

        // Old elements are overwritten when capacity is reached
        public void Add(T value)
        {
            if (Count < _array.Length)
                _array[Count++] = value;
            else
            {
                _array[_startIndex] = value;
                if (++_startIndex >= _array.Length)
                    _startIndex = 0;
            }
        }
    }

    public class DynamicCircularBuffer<T>
    {
        private T[] _array;
        private int _startIndex;

        public int Count { get; private set; }
        public int Capacity => _array.Length;

        public T this[int index]
        {
            get => _array[(_startIndex + index) % _array.Length];
            set => _array[(_startIndex + index) % _array.Length] = value;
        }

        public DynamicCircularBuffer(int initialCapacity = 2)
        {
            _array = new T[initialCapacity];
        }

        private void SetCapacity(int capacity)
        {
            T[] newArray = new T[capacity];
            if (Count > 0)
            {
                int elementsBeforeWrap = Mathf.Min(Count, _array.Length - _startIndex);
                Array.Copy(_array, _startIndex, newArray, 0, elementsBeforeWrap);
                if (elementsBeforeWrap < Count)
                    Array.Copy(_array, 0, newArray, elementsBeforeWrap, Count - elementsBeforeWrap);
            }

            _array = newArray;
            _startIndex = 0;
        }

        /// <summary>Inserts the value to the beginning of the collection.</summary>
        public void AddFirst(T value)
        {
            if (_array.Length == Count)
                SetCapacity(Mathf.Max(_array.Length * 2, 4));

            _startIndex = (_startIndex > 0) ? (_startIndex - 1) : (_array.Length - 1);
            _array[_startIndex] = value;
            Count++;
        }

        /// <summary>Adds the value to the end of the collection.</summary>
        public void Add(T value)
        {
            if (_array.Length == Count)
                SetCapacity(Mathf.Max(_array.Length * 2, 4));

            this[Count++] = value;
        }

        public void AddRange(DynamicCircularBuffer<T> other)
        {
            if (other.Count == 0)
                return;

            if (_array.Length < Count + other.Count)
                SetCapacity(Mathf.Max(_array.Length * 2, Count + other.Count));

            int insertStartIndex = (_startIndex + Count) % _array.Length;
            int elementsBeforeWrap = Mathf.Min(other.Count, _array.Length - insertStartIndex);
            int otherElementsBeforeWrap = Mathf.Min(other.Count, other._array.Length - other._startIndex);

            Array.Copy(other._array, other._startIndex, _array, insertStartIndex, Mathf.Min(elementsBeforeWrap, otherElementsBeforeWrap));
            if (elementsBeforeWrap < otherElementsBeforeWrap) // This array wrapped before the other array
                Array.Copy(other._array, other._startIndex + elementsBeforeWrap, _array, 0, otherElementsBeforeWrap - elementsBeforeWrap);
            else if (elementsBeforeWrap > otherElementsBeforeWrap) // The other array wrapped before this array
                Array.Copy(other._array, 0, _array, insertStartIndex + otherElementsBeforeWrap, elementsBeforeWrap - otherElementsBeforeWrap);

            int copiedElements = Mathf.Max(elementsBeforeWrap, otherElementsBeforeWrap);
            if (copiedElements < other.Count) // Both arrays wrapped and there's still some elements left to copy
                Array.Copy(other._array, copiedElements - otherElementsBeforeWrap, _array, copiedElements - elementsBeforeWrap, other.Count - copiedElements);

            Count += other.Count;
        }

        public T RemoveFirst()
        {
            T element = _array[_startIndex];
            _array[_startIndex] = default(T);

            if (++_startIndex == _array.Length)
                _startIndex = 0;

            Count--;
            return element;
        }

        public T RemoveLast()
        {
            int index = (_startIndex + Count - 1) % _array.Length;
            T element = _array[index];
            _array[index] = default(T);

            Count--;
            return element;
        }

        public int RemoveAll(Predicate<T> shouldRemoveElement)
        {
            return RemoveAll<T>(shouldRemoveElement, null, null);
        }

        public int RemoveAll<Y>(Predicate<T> shouldRemoveElement, Action<T, int> onElementIndexChanged, DynamicCircularBuffer<Y> synchronizedBuffer)
        {
            Y[] synchronizedArray = (synchronizedBuffer != null) ? synchronizedBuffer._array : null;
            int elementsBeforeWrap = Mathf.Min(Count, _array.Length - _startIndex);
            int removedElements = 0;
            int i = _startIndex, newIndex = _startIndex, endIndex = _startIndex + elementsBeforeWrap;
            for (; i < endIndex; i++)
            {
                if (shouldRemoveElement(_array[i]))
                    removedElements++;
                else
                {
                    if (removedElements > 0)
                    {
                        T element = _array[i];
                        _array[newIndex] = element;

                        if (synchronizedArray != null)
                            synchronizedArray[newIndex] = synchronizedArray[i];

                        if (onElementIndexChanged != null)
                            onElementIndexChanged(element, newIndex - _startIndex);
                    }

                    newIndex++;
                }
            }

            i = 0;
            endIndex = Count - elementsBeforeWrap;

            if (newIndex < _array.Length)
            {
                for (; i < endIndex; i++)
                {
                    if (shouldRemoveElement(_array[i]))
                        removedElements++;
                    else
                    {
                        T element = _array[i];
                        _array[newIndex] = element;

                        if (synchronizedArray != null)
                            synchronizedArray[newIndex] = synchronizedArray[i];

                        if (onElementIndexChanged != null)
                            onElementIndexChanged(element, newIndex - _startIndex);

                        if (++newIndex == _array.Length)
                        {
                            i++;
                            break;
                        }
                    }
                }
            }

            if (newIndex == _array.Length)
            {
                newIndex = 0;
                for (; i < endIndex; i++)
                {
                    if (shouldRemoveElement(_array[i]))
                        removedElements++;
                    else
                    {
                        if (removedElements > 0)
                        {
                            T element = _array[i];
                            _array[newIndex] = element;

                            if (synchronizedArray != null)
                                synchronizedArray[newIndex] = synchronizedArray[i];

                            if (onElementIndexChanged != null)
                                onElementIndexChanged(element, newIndex + elementsBeforeWrap);
                        }

                        newIndex++;
                    }
                }
            }

            TrimEnd(removedElements);
            if (synchronizedBuffer != null)
                synchronizedBuffer.TrimEnd(removedElements);

            return removedElements;
        }

        public void TrimStart(int trimCount, Action<T> perElementCallback = null)
        {
            TrimInternal(trimCount, _startIndex, perElementCallback);
            _startIndex = (_startIndex + trimCount) % _array.Length;
        }

        public void TrimEnd(int trimCount, Action<T> perElementCallback = null)
        {
            TrimInternal(trimCount, (_startIndex + Count - trimCount) % _array.Length, perElementCallback);
        }

        private void TrimInternal(int trimCount, int startIndex, Action<T> perElementCallback)
        {
            int elementsBeforeWrap = Mathf.Min(trimCount, _array.Length - startIndex);
            if (perElementCallback == null)
            {
                Array.Clear(_array, startIndex, elementsBeforeWrap);
                if (elementsBeforeWrap < trimCount)
                    Array.Clear(_array, 0, trimCount - elementsBeforeWrap);
            }
            else
            {
                for (int i = startIndex, endIndex = startIndex + elementsBeforeWrap; i < endIndex; i++)
                {
                    perElementCallback(_array[i]);
                    _array[i] = default(T);
                }

                for (int i = 0, endIndex = trimCount - elementsBeforeWrap; i < endIndex; i++)
                {
                    perElementCallback(_array[i]);
                    _array[i] = default(T);
                }
            }

            Count -= trimCount;
        }

        public void Clear()
        {
            int elementsBeforeWrap = Mathf.Min(Count, _array.Length - _startIndex);
            Array.Clear(_array, _startIndex, elementsBeforeWrap);
            if (elementsBeforeWrap < Count)
                Array.Clear(_array, 0, Count - elementsBeforeWrap);

            _startIndex = 0;
            Count = 0;
        }

        public int IndexOf(T value)
        {
            int elementsBeforeWrap = Mathf.Min(Count, _array.Length - _startIndex);
            int index = Array.IndexOf(_array, value, _startIndex, elementsBeforeWrap);
            if (index >= 0)
                return index - _startIndex;

            if (elementsBeforeWrap < Count)
            {
                index = Array.IndexOf(_array, value, 0, Count - elementsBeforeWrap);
                if (index >= 0)
                    return index + elementsBeforeWrap;
            }

            return -1;
        }

        public void ForEach(Action<T> action)
        {
            int elementsBeforeWrap = Mathf.Min(Count, _array.Length - _startIndex);
            for (int i = _startIndex, endIndex = _startIndex + elementsBeforeWrap; i < endIndex; i++)
                action(_array[i]);
            for (int i = 0, endIndex = Count - elementsBeforeWrap; i < endIndex; i++)
                action(_array[i]);
        }
    }
}