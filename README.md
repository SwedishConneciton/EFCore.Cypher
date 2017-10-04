# EFCore.Cypher
This project is very much a work in process.  Right now, the aim is to get the basic building blocks in place and over the next couple of days maybe some rudimental unit testing.  The core flavor of Entity Framework a labyrinth of factories and interfaces with a deep love of dependencies objects.  EF for Cypher is a rip of the relational framework.  The [open cypher project](http://www.opencypher.org/) aims to be the SQL standard for the graph world.  Cypher is very similar to SQL so many of the moving parts from the relational framework can be revamped to work with metadata for graphs.

The relational framework takes the Linq nested call expressions and mangles them down to SQL inside a database command.  Relinq is used to unravel the Linq calls into a query model.  The model despite being riddled with SQL terms can be extended (i.e. new node types) to describe the way reading clauses in Cypher are structured (i.e. a Join is just the vertex between an entity and a relationship).  The Relinq model lets us find the nodes that are involved in the stream (Queryable) and references to them (projections, predicates, etc.).  The model can be visted just like Linq expressions.  There is a visitor (CypherQueryModelVisitor) that walks the Relinq model yielding a collection of Cypher Read Only expressions.  A Read Only expression is similar to a select statement made up of one or more reading clauses and a return clause.  The Read Only expression, just like the corresponding select expression in the relational framework, has a method to grab the default query generator.  As the visitor walks the model the Read Only expression is filled and when compiled the query generator is baked into an expression.  The generator is what turns the Read Only expression into Cypher.  The whole process is giant factory of expression visitors going from Linq method calls to something that is pretty close to the Cypher grammer.
