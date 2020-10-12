﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Indexes.MapReduce;
using Raven.Documentation.Samples.Orders;


namespace Raven.Documentation.Samples.Indexes
{
    #region map_reduce_0_0
    public class Products_ByCategory : AbstractIndexCreationTask<Product, Products_ByCategory.Result>
    {
        public class Result
        {
            public string Category { get; set; }

            public int Count { get; set; }
        }

        public Products_ByCategory()
        {
            Map = products => from product in products
                              let categoryName = LoadDocument<Category>(product.Category).Name
                              select new
                              {
                                  Category = categoryName,
                                  Count = 1
                              };

            Reduce = results => from result in results
                                group result by result.Category into g
                                select new
                                {
                                    Category = g.Key,
                                    Count = g.Sum(x => x.Count)
                                };
        }
    }
    #endregion

    #region map_reduce_1_0
    public class Products_Average_ByCategory :
        AbstractIndexCreationTask<Product, Products_Average_ByCategory.Result>
    {
        public class Result
        {
            public string Category { get; set; }

            public decimal PriceSum { get; set; }

            public double PriceAverage { get; set; }

            public int ProductCount { get; set; }
        }

        public Products_Average_ByCategory()
        {
            Map = products => from product in products
                              let categoryName = LoadDocument<Category>(product.Category).Name
                              select new
                              {
                                  Category = categoryName,
                                  PriceSum = product.PricePerUnit,
                                  PriceAverage = 0,
                                  ProductCount = 1
                              };

            Reduce = results => from result in results
                                group result by result.Category into g
                                let productCount = g.Sum(x => x.ProductCount)
                                let priceSum = g.Sum(x => x.PriceSum)
                                select new
                                {
                                    Category = g.Key,
                                    PriceSum = priceSum,
                                    PriceAverage = priceSum / productCount,
                                    ProductCount = productCount
                                };
        }
    }
    #endregion

    #region map_reduce_2_0
    public class Product_Sales : AbstractIndexCreationTask<Order, Product_Sales.Result>
    {
        public class Result
        {
            public string Product { get; set; }

            public int Count { get; set; }

            public decimal Total { get; set; }
        }

        public Product_Sales()
        {
            Map = orders => from order in orders
                            from line in order.Lines
                            select new
                            {
                                Product = line.Product,
                                Count = 1,
                                Total = ((line.Quantity * line.PricePerUnit) * (1 - line.Discount))
                            };

            Reduce = results => from result in results
                                group result by result.Product into g
                                select new
                                {
                                    Product = g.Key,
                                    Count = g.Sum(x => x.Count),
                                    Total = g.Sum(x => x.Total)
                                };
        }
    }
    #endregion

    public class MapReduceIndexes
    {
        public MapReduceIndexes()
        {
            using (var store = new DocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    #region map_reduce_0_1
                    IList<Products_ByCategory.Result> results = session
                        .Query<Products_ByCategory.Result, Products_ByCategory>()
                        .Where(x => x.Category == "Seafood")
                        .ToList();
                    #endregion
                }

                using (var session = store.OpenSession())
                {
                    #region map_reduce_0_2
                    IList<Products_ByCategory.Result> results = session
                        .Advanced
                        .DocumentQuery<Products_ByCategory.Result, Products_ByCategory>()
                        .WhereEquals(x => x.Category, "Seafood")
                        .ToList();
                    #endregion
                }

                using (var session = store.OpenSession())
                {
                    #region map_reduce_1_1
                    IList<Products_Average_ByCategory.Result> results = session
                        .Query<Products_Average_ByCategory.Result, Products_Average_ByCategory>()
                        .Where(x => x.Category == "Seafood")
                        .ToList();
                    #endregion
                }

                using (var session = store.OpenSession())
                {
                    #region map_reduce_1_2
                    IList<Products_Average_ByCategory.Result> results = session
                        .Advanced
                        .DocumentQuery<Products_Average_ByCategory.Result, Products_Average_ByCategory>()
                        .WhereEquals(x => x.Category, "Seafood")
                        .ToList();
                    #endregion
                }

                using (var session = store.OpenSession())
                {
                    #region map_reduce_2_1
                    IList<Product_Sales.Result> results = session
                        .Query<Product_Sales.Result, Product_Sales>()
                        .ToList();
                    #endregion
                }

                using (var session = store.OpenSession())
                {
                    #region map_reduce_2_2
                    IList<Product_Sales.Result> results = session
                        .Advanced
                        .DocumentQuery<Product_Sales.Result, Product_Sales>()
                        .ToList();
                    #endregion
                }
            }
        }
    }

    public class Product_Sales_ByDate : AbstractIndexCreationTask<Order, Product_Sales_ByDate.Result>
    {
        public class Result
        {
            public string Product { get; set; }

            public DateTime Date { get; set; }

            public int Count { get; set; }

            public decimal Total { get; set; }
        }

        
        #region map_reduce_3_0
        public Product_Sales_ByDate()
        {
            Map = orders => from order in orders
                            from line in order.Lines
                            select new
                            {
                                Product = line.Product,
                                Date = new DateTime(order.OrderedAt.Year,
                                                    order.OrderedAt.Month,
                                                    order.OrderedAt.Day),
                                Count = 1,
                                Total = ((line.Quantity * line.PricePerUnit) * (1 - line.Discount))
                            };

            Reduce = results => from result in results
                                group result by new { result.Product, result.Date } into g
                                select new
                                {
                                    Product = g.Key.Product,
                                    Date = g.Key.Date,
                                    Count = g.Sum(x => x.Count),
                                    Total = g.Sum(x => x.Total)
                                };

            OutputReduceToCollection = "DailyProductSales";
            PatternReferencesCollectionName = "DailyProductSales/References";
            PatternForOutputReduceToCollectionReferences = x => $"sales/daily/{x.Date:yyyy-MM-dd}";
        }
        #endregion
    }

    /*
    #region map_reduce_reference_doc
    {
        "Product": "products/77-A",
        "Date": "1998-05-06T00:00:00.0000000",
        "Count": 1,
        "Total": 26,
        "@metadata": {
            "@collection": "DailyProductSales",
            "@flags": "Artificial, FromIndex"
        }
    }
    #endregion
    */

    public class MapReduce_Output_OrderProduct_ByCount : AbstractIndexCreationTask<Order, MapReduce_Output_OrderProduct_ByCount.Result>
    {
        public class Result
        {
            public string Product;
            public int Count;
            public int NumOrders;
        }
        
        #region map_reduce_4_0
        public MapReduce_Output_OrderProduct_ByCount()
        {
            Map = orders => from order in orders
                            let referenceDocuments = LoadDocument<OutputReduceToCollectionReference>(
                                                     $"sales/daily/{order.OrderedAt}", 
                                                     "DailyProductSales/References")
                            from refDoc in referenceDocuments.ReduceOutputs
                            let outputDoc = LoadDocument<OutputDocument>(refDoc)
                            select new Result {
                                Product = outputDoc.Product,
                                Count = outputDoc.Count,
                                NumOrders = 1
                            };

            Reduce = results => from r in results
                                group r by new { r.Count, r.Product }
                                into g
                                select new { 
                                    Product = g.Key.Product,
                                    Count = g.Key.Count,
                                    NumOrders = g.Sum(x => x.NumOrders)
                                };
        }
        #endregion
    }

    public class OutputDocument {
        public string Product;
        public int Count;
        public int NumOrders;
    }

    /*
    class foo 
    {
        #region syntax_0
        string OutputReduceToCollection;

        string PatternReferencesCollectionName;

        // Using IndexDefinition
        string PatternForOutputReduceToCollectionReferences;

        // Inheriting from AbstractGenericIndexCreationTask<TReduceResult>
        Expression<Func<TReduceResult, string>> PatternForOutputReduceToCollectionReferences;
        #endregion

        #region syntax_1
        public class OutputReduceToCollectionReference
        {
            public string Id { get; set; }
            public List<string> ReduceOutputs { get; set; }
        }
        #endregion

        private class TReduceResult
        {
        }
    }
    */
}
