﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace com.espertech.esper.compat.collections
{
    /// <summary>
    /// An efficient subsection of an ordered or sorted dictionary.  Designed for traversal and lookup, but not
    /// necessarily efficient at counting elements.
    /// </summary>
    public class SubmapDictionary<K,V> : IDictionary<K,V>
    {
        private readonly OrderedDictionary<K, V> _subDictionary;
        private Bound _lower;
        private Bound _upper;

        private readonly Func<K, bool> _lowerTest;
        private readonly Func<K, bool> _upperTest;

        internal SubmapDictionary(OrderedDictionary<K, V> subDictionary, Bound lower, Bound upper)
        {
            _subDictionary = subDictionary;
            _lower = lower;
            _upper = upper;

            if (_subDictionary == null)
            {
                _lowerTest = k1 => false;
                _upperTest = k1 => false;
            }
            else
            {
                var keyComparer = _subDictionary.KeyComparer ?? new DefaultComparer();
                _lowerTest = MakeLowerTest(keyComparer);
                _upperTest = MakeUpperTest(keyComparer);
            }
        }

        private Func<K, bool> MakeUpperTest(IComparer<K> keyComparer)
        {
            if (_upper.HasValue)
            {
                if (_upper.IsInclusive)
                {
                    return k1 => keyComparer.Compare(k1, _upper.Key) <= 0;
                }
                else
                {
                    return k1 => keyComparer.Compare(k1, _upper.Key) < 0;
                }
            }
            else
            {
                return k1 => true;
            }
        }

        private Func<K, bool> MakeLowerTest(IComparer<K> keyComparer)
        {
            if (_lower.HasValue)
            {
                if (_lower.IsInclusive)
                {
                    return k1 => keyComparer.Compare(k1, _lower.Key) >= 0;
                }
                else
                {
                     return k1 => keyComparer.Compare(k1, _lower.Key) > 0;
                }
            }
            else
            {
                return k1 => true;
            }
        }

        #region Implementation of IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Implementation of IEnumerable<out KeyValuePair<K,V>>

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            IEnumerable<KeyValuePair<K, V>> enumerator = 
                _lower.HasValue
                ? _subDictionary.GetTail(_lower.Key, _lower.IsInclusive)
                : _subDictionary;
            
            return enumerator
                .TakeWhile(pair => _upperTest.Invoke(pair.Key))
                .GetEnumerator();
        }

        #endregion

        #region Implementation of ICollection<KeyValuePair<K,V>>

        public void Add(KeyValuePair<K, V> item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            if (_lowerTest.Invoke(item.Key) && _upperTest.Invoke(item.Key))
            {
                return _subDictionary.Contains(item);
            }

            return false;
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                array[arrayIndex++] = enumerator.Current;
            }
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            throw new NotSupportedException();
        }

        public int Count
        {
            get
            {
                IEnumerable<KeyValuePair<K, V>> iterator;

                if (_lower.HasValue)
                    iterator = _subDictionary.GetTail(_lower.Key, _lower.IsInclusive);
                else
                    iterator = _subDictionary;

                return iterator
                    .TakeWhile(pair => _upperTest.Invoke(pair.Key))
                    .Count();
            }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        #endregion

        #region Implementation of IDictionary<K,V>

        public bool ContainsKey(K key)
        {
            if (_lowerTest.Invoke(key) && _upperTest.Invoke(key))
            {
                return _subDictionary.ContainsKey(key);
            }

            return false;
        }

        public void Add(K key, V value)
        {
            throw new NotSupportedException();
        }

        public bool Remove(K key)
        {
            throw new NotSupportedException();
        }

        public bool TryGetValue(K key, out V value)
        {
            if (_lowerTest.Invoke(key) && _upperTest.Invoke(key))
            {
                return _subDictionary.TryGetValue(key, out value);
            }

            value = default(V);
            return false;
        }

        public V this[K key]
        {
            get
            {
                if (_lowerTest.Invoke(key) && _upperTest.Invoke(key))
                {
                    return _subDictionary[key];
                }

                throw new KeyNotFoundException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public ICollection<K> Keys
        {
            get
            {
                IEnumerable<KeyValuePair<K, V>> iterator;

                if (_lower.HasValue)
                    iterator = _subDictionary.GetTail(_lower.Key, _lower.IsInclusive);
                else
                    iterator = _subDictionary;
                
                return iterator
                    .TakeWhile(pair => _upperTest.Invoke(pair.Key))
                    .Select(pair => pair.Key)
                    .ToList();
            }
        }

        public ICollection<V> Values
        {
            get
            {
                IEnumerable<KeyValuePair<K, V>> iterator;

                if (_lower.HasValue)
                    iterator = _subDictionary.GetTail( _lower.Key, _lower.IsInclusive);
                else
                    iterator = _subDictionary;

                return iterator
                    .TakeWhile(pair => _upperTest.Invoke(pair.Key))
                    .Select(pair => pair.Value)
                    .ToList();
            }
        }

        #endregion

        internal struct Bound
        {
            internal K Key;
            internal bool IsInclusive;
            internal bool HasValue;
        }

        internal class DefaultComparer : Comparer<K>
        {
            #region Overrides of Comparer<K>

            public override int Compare(K x, K y)
            {
                return ((IComparable) x).CompareTo(y);
            }

            #endregion
        }
    }
}
