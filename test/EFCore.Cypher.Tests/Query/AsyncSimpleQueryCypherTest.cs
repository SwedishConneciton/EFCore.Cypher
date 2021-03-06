// Based on https://github.com/aspnet/EntityFrameworkCore
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.TestUtilities.FakeProvider;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <summary>
    /// Queryable to Cypher
    /// </summary>
    /// <remarks>No checks for Cypher to CLR</remarks>
    public class AsyncSimpleQueryCypherTest {

        public DbContextOptions DbContextOptions { get; } = CypherTestHelpers.Options("Flash=BarryAllen");


        /// <summary>
        /// No linq just the database set
        /// </summary>
        [Fact]
        public void Select_warehouse_without_linq() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN \"w\".\"Location\", \"w\".\"Size\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select by identity
        /// </summary>
        [Fact]
        public void Select_warehouse_identity() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => w)
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN \"w\".\"Location\", \"w\".\"Size\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select with flat object assignment
        /// </summary>
        [Fact]
        public void Select_warehouse_with_object() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => new { Place = w.Location, Status = 1})
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN \"w\".\"Location\" AS \"Place\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select with nested object assignment
        /// </summary>
        [Fact]
        public void Select_warehouse_with_nested() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => new { w.Location, Size = new { w.Size }})
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN \"w\".\"Location\", \"w\".\"Size\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select with empty object
        /// </summary>
        [Fact]
        public void Select_warehouse_with_empty() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => new { })
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN 1",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select with object having a single literal
        /// </summary>
        [Fact]
        public void Select_warehouse_with_literal() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => new { Size = 1 })
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN 1",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select with object having a conditional
        /// </summary>
        [Fact]
        public void Select_warehouse_with_conditional() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => new { IsBig = w.Size > 100 })
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN \"w\".\"Size\" > 100 AS \"IsBig\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Only select with object having a conditional case
        /// </summary>
        [Fact]
        public void Select_warehouse_with_conditional_case() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Select(w => new { IsBig = w.Size > 100 ? "Giant" : "Ant" })
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse) RETURN CASE\r\n    WHEN \"w\".\"Size\" > 100\r\n    THEN 'Giant' ELSE 'Ant'\r\nEND AS \"IsBig\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Where one property equals constant with default return
        /// </summary>
        [Fact]
        public void Where_single_property_equal() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Where(w => w.Size == 100)
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse)\r\nWHERE \"w\".\"Size\" = 100 RETURN \"w\".\"Location\", \"w\".\"Size\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Single join
        /// </summary>
        [Fact]
        public void Join_single() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Join(
                        ctx.Things, 
                        ctx.Owning,
                        (o) => o,
                        (i) => i, 
                        (o, i, r) => new {o, i, r}
                    )
                    .AsCypher();

                Assert.Equal(
                    "MATCH (o:Warehouse)\r\nMATCH (o)-[r:\"OWNS\"]->(i:Thing) RETURN \"o\".\"Location\", \"o\".\"Size\", \"r\".\"Partial\", \"i\".\"Number\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Two joins back-to-back
        /// </summary>
        [Fact]
        public void Join_double() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Join(
                        ctx.Things, 
                        ctx.Owning,
                        (w) => w,
                        (t) => t, 
                        (w, t, r) => new {w, t, r}
                    )
                    .Join(
                        ctx.Persons,
                        ctx.Supervising,
                        (o) => o.w,
                        (p) => p,
                        (o, p, r) => new {o, p, r}
                    )
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse)\r\nMATCH (w)-[r:\"OWNS\"]->(t:Thing)\r\nMATCH (w)-[r0:\"Supervise\"]->(p:Person) RETURN \"w\".\"Location\", \"w\".\"Size\", \"r\".\"Partial\", \"t\".\"Number\", \"r0\".\"Certified\", \"p\".\"Name\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Select many without correlation
        /// </summary>
        [Fact]
        public void SelectMany_single() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .SelectMany(
                        w => ctx.Persons,
                        (w, p) => new {w, p}
                    )
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse)\r\nMATCH (p:Person) RETURN \"w\".\"Location\", \"w\".\"Size\", \"p\".\"Name\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Projection with join
        /// </summary>
        /// <remarks>
        /// Test case resulted in the override on the required 
        /// materialization visitor which demote selectors so that 
        /// the normal expectations on the demotion/promotion of query 
        /// sources is safe.
        /// </remarks>
        [Fact]
        public void Join_simple_projection() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Join(
                        ctx.Things, 
                        ctx.Owning,
                        (w) => w,
                        (t) => t, 
                        (w, t, r) => t
                    )
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse)\r\nMATCH (w)-[r:\"OWNS\"]->(t:Thing) RETURN \"t\".\"Number\"",
                    cypher
                );
            }
        }

        /// <summary>
        /// Join then select many
        /// </summary>
        [Fact]
        public void Join_select_many() {
            using (var ctx = new CypherFaceDbContext(DbContextOptions)) {
                var cypher = ctx.Warehouses
                    .Join(
                        ctx.Things, 
                        ctx.Owning,
                        (w) => w,
                        (t) => t, 
                        (w, t, r) => new {w, r}
                    )
                    .SelectMany(
                        x => ctx.Persons,
                        (x, p) => new {x, p}
                    )
                    .AsCypher();

                Assert.Equal(
                    "MATCH (w:Warehouse)\r\nMATCH (w)-[r:\"OWNS\"]->(t:Thing)\r\nMATCH (p:Person) RETURN \"w\".\"Location\", \"w\".\"Size\", \"r\".\"Partial\", \"p\".\"Name\"",
                    cypher
                );
            }
        }
    }
}