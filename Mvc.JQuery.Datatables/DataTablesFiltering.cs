using System;
using System.Collections.Generic;
using System.Linq;
using Mvc.JQuery.Datatables.DynamicLinq;
using Mvc.JQuery.Datatables.Reflection;

namespace Mvc.JQuery.Datatables
{
    internal class DataTablesFiltering
    {
        public IQueryable<T> ApplyFiltersAndSort<T>(DataTablesParam dtParameters, IQueryable<T> data, DataTablesPropertyInfo[] columns)
        {
            if (!String.IsNullOrEmpty(dtParameters.sSearch))
            {
                var parts = new List<string>();
                var parameters = new List<object>();
                for (var i = 0; i < dtParameters.iColumns; i++)
                {
                    if (dtParameters.bSearchable[i])
                    {
                        try
                        {
                            parts.Add(GetFilterClause(dtParameters.sSearch, columns[i], parameters));
                        }
                        catch (Exception)
                        {
                            //if the clause doesn't work, skip it!
                        }
                    }
                }
                var values = parts.Where(p => p != null);
                data = data.Where(string.Join(" or ", values), parameters.ToArray());
            }
            for (int i = 0; i < dtParameters.sSearchValues.Count; i++)
            {
                if (dtParameters.bSearchable[i])
                {
                    var searchColumn = dtParameters.sSearchValues[i];
                    if (!string.IsNullOrWhiteSpace(searchColumn))
                    {
                        DataTablesPropertyInfo column = FindColumn(dtParameters, columns, i);
                        var parameters = new List<object>();
                        var filterClause = GetFilterClause(searchColumn, column, parameters);
                        if (string.IsNullOrWhiteSpace(filterClause) == false)
                        {
                            data = data.Where(filterClause, parameters.ToArray());
                        }
                    }
                }
            }
            string sortString = "";
            for (int i = 0; i < dtParameters.iSortingCols; i++)
            {
                int columnNumber = dtParameters.iSortCol[i];
                DataTablesPropertyInfo column = FindColumn(dtParameters, columns, columnNumber);
                string columnName = column.PropertyInfo.Name;
                string sortDir = dtParameters.sSortDir[i];
                if (i != 0)
                    sortString += ", ";
                sortString += columnName + " " + sortDir;
            }
            if (string.IsNullOrWhiteSpace(sortString))
            {
                sortString = columns[0].PropertyInfo.Name;
            }
            data = data.OrderBy(sortString);


            return data;
        }

        private DataTablesPropertyInfo FindColumn(DataTablesParam dtParameters, DataTablesPropertyInfo[] columns, int i)
        {
            if (dtParameters.sColumnNames.Any())
            {
                return columns.First(x => x.PropertyInfo.Name == dtParameters.sColumnNames[i]);
            }
            else
            {
                return columns[i];
            }
        }

        public delegate string ReturnedFilteredQueryForType(
            string query, string columnName, DataTablesPropertyInfo columnType, List<object> parametersForLinqQuery);


        private static readonly List<ReturnedFilteredQueryForType> Filters = new List<ReturnedFilteredQueryForType>()
        {
            Guard(IsBoolType, TypeFilters.BoolFilter),
            Guard(IsDateTimeType, TypeFilters.DateTimeFilter),
            Guard(IsDateTimeOffsetType, TypeFilters.DateTimeOffsetFilter),
            Guard(IsNumericType, TypeFilters.NumericFilter),
            Guard(IsEnumType, TypeFilters.EnumFilter),
            Guard(IsStringType, TypeFilters.StringFilter),
        };


        public delegate string GuardedFilter(
            string query, string columnName, DataTablesPropertyInfo columnType, List<object> parametersForLinqQuery);

        private static ReturnedFilteredQueryForType Guard(Func<DataTablesPropertyInfo, bool> guard, GuardedFilter filter)
        {
            return (q, c, t, p) =>
            {
                if (!guard(t))
                {
                    return null;
                }
                return filter(q, c, t, p);
            };
        }

        public static void RegisterFilter<T>(GuardedFilter filter)
        {
            Filters.Add(Guard(arg => arg is T, filter));
        }

        private static string GetFilterClause(string query, DataTablesPropertyInfo column, List<object> parametersForLinqQuery)
        {
            var isCollection = column.Type.IsGenericType && column.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
            Func<string, string> filterClause = (queryPart) =>
                                                Filters.Select(
                                                    f => f(queryPart, isCollection ? "it" : column.PropertyInfo.Name, column, parametersForLinqQuery))
                                                        .FirstOrDefault(filterPart => filterPart != null) ?? "";

            var queryParts = query.Split('|').Select(filterClause).Where(fc => fc != "").ToArray();
            if (queryParts.Any())
            {
                if (isCollection)
                {
                    return String.Format("{0}.Any(({1}))", column.PropertyInfo.Name, string.Join(") OR (", queryParts));
                }
                else
                {
                    return "(" + string.Join(") OR (", queryParts) + ")";
                }
            }
            return null;
        }


        public static bool IsNumericType(DataTablesPropertyInfo propertyInfo)
        {
            return IsNumericType(propertyInfo.Type) ||
                (propertyInfo.Type.IsGenericType && propertyInfo.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) && IsNumericType(propertyInfo.Type.GetGenericArguments()[0]));
        }

        private static bool IsNumericType(Type type)
        {
            if (type == null || type.IsEnum)
            {
                return false;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                case TypeCode.Object:
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>))
                    {
                        return IsNumericType(Nullable.GetUnderlyingType(type));
                    }
                    return false;
            }
            return false;
        }

        public static bool IsEnumType(DataTablesPropertyInfo propertyInfo)
        {
            return propertyInfo.Type.IsEnum || (propertyInfo.Type.IsGenericType && propertyInfo.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) && propertyInfo.Type.GetGenericArguments()[0].IsEnum);
        }

        public static bool IsBoolType(DataTablesPropertyInfo propertyInfo)
        {
            return propertyInfo.Type == typeof(bool) || propertyInfo.Type == typeof(bool?) ||
                propertyInfo.Type == typeof(IEnumerable<bool>) || propertyInfo.Type == typeof(IEnumerable<bool?>);
        }
        public static bool IsDateTimeType(DataTablesPropertyInfo propertyInfo)
        {
            return propertyInfo.Type == typeof(DateTime) || propertyInfo.Type == typeof(DateTime?) ||
                propertyInfo.Type == typeof(IEnumerable<DateTime>) || propertyInfo.Type == typeof(IEnumerable<DateTime?>);
        }
        public static bool IsDateTimeOffsetType(DataTablesPropertyInfo propertyInfo)
        {
            return propertyInfo.Type == typeof(DateTimeOffset) || propertyInfo.Type == typeof(DateTimeOffset?) ||
                propertyInfo.Type == typeof(IEnumerable<DateTimeOffset>) || propertyInfo.Type == typeof(IEnumerable<DateTimeOffset?>);
        }

        public static bool IsStringType(DataTablesPropertyInfo propertyInfo)
        {
            return propertyInfo.Type == typeof(string) || propertyInfo.Type == typeof(IEnumerable<string>);
        }

    }
}