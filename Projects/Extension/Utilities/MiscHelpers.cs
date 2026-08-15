using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Utilities
{
    public class MiscHelpers
    {
        public delegate bool Validator<T>(T item);
        public delegate IComparable DistanceCalculator<T1, T2>(T1 src, T2 target);
        public static List<T1> FindNearest<T1, T2>(
            IEnumerable<T1> list, 
            T2 target, 
            DistanceCalculator<T1, T2> distanceCalculator,
            int nearestCount = 1,
            Validator<T1> validator = null
            )
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list), "源集合不能为null");
            if (distanceCalculator == null)
                throw new ArgumentNullException(nameof(distanceCalculator), "距离计算器不能为null");
            if (nearestCount < 1)
                throw new ArgumentOutOfRangeException(nameof(nearestCount), "最近元素数量必须≥1");

            // 2. 过滤有效元素（应用验证器）
            var validItems = new List<T1>();
            foreach (var item in list)
            {
                // 验证器为null时，所有元素都有效；否则按验证规则过滤
                if (validator == null || validator(item))
                {
                    validItems.Add(item);
                }
            }

            // 3. 处理无有效元素的情况
            if (validItems.Count == 0)
                return new List<T1>();

            // 4. 计算距离并按距离升序排序（核心逻辑）
            // 优化：用 Tuple 存储元素+距离，避免重复计算
            var itemsWithDistance = validItems
                .Select(item => new { Item = item, Distance = distanceCalculator(item, target) })
                .OrderBy(x => x.Distance); // 升序：距离越小越靠前

            // 5. 取前 nearestCount 个元素
            var result = itemsWithDistance
                .Take(nearestCount)
                .Select(x => x.Item)
                .ToList();

            return result;
        }
        public static List<T> SelectRandom<T>(
            IEnumerable<T> list,
            int count = 1,
            Validator<T> validator = null
            )
        {
            // 1. 核心参数校验（补充你缺失的list判空）
            if (list == null)
                throw new ArgumentNullException(nameof(list), "源集合不能为null");
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "选取数量必须≥1");

            // 2. 过滤有效元素（应用验证器）
            var validItems = new List<T>();
            foreach (var item in list)
            {
                if (validator == null || validator(item))
                {
                    validItems.Add(item);
                }
            }

            // 3. 处理无有效元素的情况
            if (validItems.Count == 0)
                return new List<T>();

            // 4. 修正选取数量：如果有效元素不足count，只取所有有效元素
            int actualCount = Math.Min(count, validItems.Count);

            // 5. 核心逻辑：无重复随机选取（洗牌法，性能优）
            // 拷贝有效元素避免修改原列表，然后洗牌取前N个
            var shuffledItems = ScenarioClass.Shuffle(validItems);

            // 6. 取前actualCount个元素作为结果
            var result = shuffledItems.Take(actualCount).ToList();

            return result;
        }
    }
}
