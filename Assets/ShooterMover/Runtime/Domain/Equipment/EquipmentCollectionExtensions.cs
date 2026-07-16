using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Equipment
{
    internal static class EquipmentCollectionExtensions
    {
        public static int BinarySearch<T>(
            this ReadOnlyCollection<T> values,
            T item,
            IComparer<T> comparer)
        {
            int low = 0;
            int high = values.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = comparer.Compare(values[middle], item);
                if (comparison == 0)
                {
                    return middle;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return ~low;
        }
    }
}
