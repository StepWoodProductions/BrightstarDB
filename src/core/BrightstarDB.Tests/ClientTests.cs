﻿#if !PORTABLE
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using BrightstarDB.Client;
using BrightstarDB.Rdf;
using BrightstarDB.Storage;
using BrightstarDB.Tests.EntityFramework;
using NUnit.Framework;
using VDS.RDF;
using VDS.RDF.Parsing;
using NTriplesParser = BrightstarDB.Rdf.NTriplesParser;

namespace BrightstarDB.Tests
{
    [TestFixture("type=rest;endpoint=http://localhost:8090/brightstar")]
    [TestFixture("type=embedded;storesDirectory={0}")]
    public class ClientTests : ClientTestBase
    {
        private readonly string _connectionString;
#if PORTABLE
        private IPersistenceManager _persistenceManager;
#endif


        public ClientTests(string connectionString)
        {
            _connectionString = String.Format(connectionString, Configuration.StoreLocation);
        }

        private IBrightstarService GetClient()
        {
            return BrightstarService.GetClient(_connectionString);
        }


        private void CopyTestDataToImportFolder(string testDataFileName, string targetFileName = null)
        {
#if PORTABLE
            using (var srcStream = _persistenceManager.GetInputStream(Configuration.DataLocation + testDataFileName))
            {
                var targetDir = Path.Combine(Configuration.StoreLocation, "import");
                var targetPath = Path.Combine(targetDir, (targetFileName ?? testDataFileName));
                if (!_persistenceManager.DirectoryExists(targetDir)) _persistenceManager.CreateDirectory(targetDir);
                if (_persistenceManager.FileExists(targetPath)) _persistenceManager.DeleteFile(targetPath);
                using (var targetStream = _persistenceManager.GetOutputStream(targetPath, FileMode.CreateNew))
                {
                    srcStream.CopyTo(targetStream);
                }
            }
#else
            var importFile = new FileInfo(Path.Combine(Configuration.DataLocation, testDataFileName));
            var targetDir = new DirectoryInfo(Path.Combine(Configuration.StoreLocation, "import"));
            if (!targetDir.Exists)
            {
                targetDir.Create();
            }
            importFile.CopyTo(Path.Combine(targetDir.FullName, targetFileName ?? testDataFileName), true);
#endif
        }
        [TestFixtureSetUp]
        public void SetUp()
        {
            if (_connectionString.Contains("type=rest"))
            {
                StartService();
            }
#if PORTABLE
        _persistenceManager = new PersistenceManager();
#endif
            CopyTestDataToImportFolder("graph_triples.nt");

        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            if (_connectionString.Contains("type=rest"))
            {
                CloseService();
            }
        }

        [Test]
        public void TestCreateStore()
        {
            var bc = GetClient();
            var sid = Guid.NewGuid().ToString();
            bc.CreateStore(sid);
        }

        [Test]
        public void TestInvalidStoreNames()
        {
            var bc = GetClient();
            try
            {
                bc.CreateStore(null);
                Assert.Fail("Expected ArgumentNullException");
            } catch(ArgumentNullException)
            {
                // Expected
            }

            try
            {
                bc.CreateStore(String.Empty);
                Assert.Fail("Expected ArgumentException (empty string)");
            } catch(ArgumentException)
            {
                // Expected
            }

            try
            {
                bc.CreateStore("This is\\an invalid\\store name");
                Assert.Fail("Expected ArgumentException (backslash in name)");
            }catch(ArgumentException)
            {
                //Expected
            }

            try
            {
                bc.CreateStore("This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.This is an invalid store name because it is too long.");
                Assert.Fail("Expected ArgumentException (name too long)");
            } catch(ArgumentException)
            {
                // Expected
            }
        }

        [Test]
        public void TestIfStoreExistsFalseWhenNoStoreCreated()
        {
            var bc = GetClient();
            var sid = Guid.NewGuid().ToString();
            var exists = bc.DoesStoreExist(sid);
            Assert.IsFalse(exists);
        }

        [Test]
        public void TestIfStoreExistsTrueAfterStoreCreated()
        {
            var bc = GetClient();
            var sid = Guid.NewGuid().ToString();
            bc.CreateStore(sid);
            var exists = bc.DoesStoreExist(sid);
            Assert.IsTrue(exists);
        }

        [Test]
        [ExpectedException(typeof(BrightstarClientException))]
        public void TestCreateDuplicateStoreFails()
        {

            var bc = GetClient();
            var sid = Guid.NewGuid().ToString();
            bc.CreateStore(sid);
            bc.CreateStore(sid);
        }

        [Test]
        public void TestDeleteStore()
        {
            var bc = GetClient();

            // create store
            var sid = Guid.NewGuid().ToString();
            bc.CreateStore(sid);

            // check it is there
            var stores = bc.ListStores();
            Assert.AreEqual(1, stores.Where(s => s.Equals(sid)).Count());

            // delete store
            bc.DeleteStore(sid);

            // check it is gone
            stores = bc.ListStores();
            Assert.AreEqual(0, stores.Where(s => s.Equals(sid)).Count());
        }

        [Test]
        public void TestListStores()
        {
            var bc = GetClient();
            var stores = bc.ListStores();
            Assert.IsTrue(stores.Count() > 0);
        }

        [Test]
        public void TestQuery()
        {
            var client = GetClient();
            var storeName  = "Client.TestQuery_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);
            client.ExecuteQuery(storeName, "SELECT ?s WHERE { ?s ?p ?o }");
        }

        [Test]
        public void TestQueryIfNotModifiedSince()
        {
            var client = GetClient();
            if (!(client is BrightstarRestClient)) return;
            var storeName = "Client.TestQueryIfNotModifiedSince_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);
            client.ExecuteQuery(storeName, "SELECT ?s WHERE { ?s ?p ?o }");
            var lastResponseTime = client.LastResponseTimestamp;
            Assert.IsNotNull(lastResponseTime);
            try
            {
                client.ExecuteQuery(storeName, "SELECT ?s WHERE {?s ?p ?o}", lastResponseTime);
                Assert.Fail("Expected a BrightstarClientException");
            }
            catch (BrightstarClientException clientException)
            {
                //Assert.AreEqual(typeof (BrightstarStoreNotModifiedException).FullName,
                //                clientException.InnerException.Type);
                Assert.AreEqual("Store not modified", clientException.Message);
            }
        }


        public void TestLargeQueryResult()
        {
            
        }


        // Tel: 240439

        /*
        [Test]
        public void TestGetStoreData()
        {
            var bc = GetClient();
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            var triples = bc.GetStoreData(storeName);
            var memoryStream = new MemoryStream();
            triples.CopyTo(memoryStream);
            Assert.AreEqual(0, memoryStream.Length);
        }
        */

        [Test]
        public void TestTransactionAddStatements()
        {
            var bc = GetClient();
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            const string triplesToAdd = @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2> .";

            var jobInfo = bc.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = triplesToAdd}, label:"Add Triples");

            Assert.IsNotNull(jobInfo);
            Assert.That(jobInfo.Label, Is.EqualTo("Add Triples"));

            while (!jobInfo.JobCompletedOk && !jobInfo.JobCompletedWithErrors)
            {
                Thread.Sleep(50);
                jobInfo = bc.GetJobInfo(storeName, jobInfo.JobId);
                Assert.That(jobInfo.Label, Is.EqualTo("Add Triples"));
            }

            //var triples = bc.GetStoreData(storeName);
            //var memoryStream = new MemoryStream();
            //triples.CopyTo(memoryStream);
            //memoryStream.Flush();
            //Assert.IsTrue(0 < memoryStream.Length);
        }

        [Test] public void TestTransactiondDeleteStatements()
        {
            var bc = GetClient();
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            const string triplesToAdd = @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2>.";
            
            var jobInfo = bc.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData= triplesToAdd});

            Assert.IsNotNull(jobInfo);

            while (!jobInfo.JobCompletedOk && !jobInfo.JobCompletedWithErrors)
            {
                Thread.Sleep(50);
                jobInfo = bc.GetJobInfo(storeName, jobInfo.JobId);
            }

            //var triples = bc.GetStoreData(storeName);
            //var memoryStream = new MemoryStream();
            //triples.CopyTo(memoryStream);
            //Assert.IsTrue(0 < memoryStream.Length);

            const string deletePatterns = @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2>.";

            jobInfo = bc.ExecuteTransaction(storeName, new UpdateTransactionData {DeletePatterns = deletePatterns});

            while (!jobInfo.JobCompletedOk && !jobInfo.JobCompletedWithErrors)
            {
                Thread.Sleep(50);
                jobInfo = bc.GetJobInfo(storeName, jobInfo.JobId);
            }

            //triples = bc.GetStoreData(storeName);
            //memoryStream = new MemoryStream();
            //triples.CopyTo(memoryStream);
            //Assert.AreEqual(0, memoryStream.Length);
        }

        [Test]
        public void TestSparqlQuery()
        {
            var bc = GetClient();
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            const string triplesToAdd = @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2>.";

            var jobInfo = bc.ExecuteTransaction(storeName, new UpdateTransactionData {InsertData = triplesToAdd});

            Assert.IsNotNull(jobInfo);

            while (!jobInfo.JobCompletedOk && !jobInfo.JobCompletedWithErrors)
            {
                Thread.Sleep(50);
                jobInfo = bc.GetJobInfo(storeName, jobInfo.JobId);
            }

            //var triples = bc.GetStoreData(storeName);
            //var memoryStream = new MemoryStream();
            //triples.CopyTo(memoryStream);
            //Assert.IsTrue(0 < memoryStream.Length);

            // do query
            var result = bc.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }");
            Assert.IsNotNull(result);
        }

        [Test]
        public void TestSparqlQueryWithDefaultGraph()
        {
            var client = GetClient();
            var storeName = "SparqlQueryWithDefaultGraph_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            var triplesToAdd = new StringBuilder();
            triplesToAdd.AppendLine(@"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2>.");
            triplesToAdd.AppendLine(
                @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource3> <http://example.org/graph1> .");

            var jobInfo = client.ExecuteTransaction(storeName,
                                                    new UpdateTransactionData {InsertData = triplesToAdd.ToString()});
            Assert.IsNotNull(jobInfo);
            Assert.IsTrue(jobInfo.JobCompletedOk);

            // do query
            var resultStream = client.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }", "http://example.org/graph1");
            var result = XDocument.Load(resultStream);
            var rows = result.SparqlResultRows().ToList();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(new Uri("http://example.org/resource3"), rows[0].GetColumnValue("o"));

            // Do a query over the normal default graph
            resultStream = client.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }");
            result = XDocument.Load(resultStream);
            rows = result.SparqlResultRows().ToList();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(new Uri("http://example.org/resource2"), rows[0].GetColumnValue("o"));

            // Issue #221: It should be possible to pass NULL for the default graph IRI without causing and invalid query POST
            resultStream = client.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }", (string)null, null, SparqlResultsFormat.Xml, RdfFormat.RdfXml);
            result = XDocument.Load(resultStream);
            rows = result.SparqlResultRows().ToList();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(new Uri("http://example.org/resource2"), rows[0].GetColumnValue("o"));

        }

        [Test]
        public void TestSparqlQueryWithDefaultGraphs()
        {
            var client = GetClient();
            var storeName = "SparqlQueryWithDefaultGraphs_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            var triplesToAdd = new StringBuilder();
            triplesToAdd.AppendLine(@"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2>.");
            triplesToAdd.AppendLine(
                @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource3> <http://example.org/graph1> .");
            triplesToAdd.AppendLine(
                @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource4> <http://example.org/graph2> .");

            var jobInfo = client.ExecuteTransaction(storeName,
                                                    new UpdateTransactionData {InsertData = triplesToAdd.ToString()});
            Assert.IsNotNull(jobInfo);
            Assert.IsTrue(jobInfo.JobCompletedOk);

            // do query using graph1 and graph2 as the default
            var resultStream = client.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }", 
                new[] {"http://example.org/graph1", "http://example.org/graph2"});
            var result = XDocument.Load(resultStream);
            var rows = result.SparqlResultRows().ToList();
            Assert.AreEqual(2, rows.Count);
            var expected = new[] {new Uri("http://example.org/resource3"), new Uri("http://example.org/resource4")};
            Assert.IsTrue(expected.Contains(rows[0].GetColumnValue("o")));
            Assert.IsTrue(expected.Contains(rows[1].GetColumnValue("o")));

            // Do a query over the normal default graph
            resultStream = client.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }");
            result = XDocument.Load(resultStream);
            rows = result.SparqlResultRows().ToList();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(new Uri("http://example.org/resource2"), rows[0].GetColumnValue("o"));
            
        }
        [Test]
        public void TestSparqlXDocumentExtensions()
        {
            var bc = GetClient();
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            const string triplesToAdd = @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2> .
                      <http://example.org/resource14> <http://example.org/property1> ""30""^^<http://www.w3.org/2001/XMLSchema#integer> . ";

            var jobInfo = bc.ExecuteTransaction(storeName, new UpdateTransactionData {InsertData = triplesToAdd});

            Assert.IsNotNull(jobInfo);
            Assert.IsTrue(jobInfo.JobCompletedOk);

           // var triples = bc.GetStoreData(storeName);
            //var memoryStream = new MemoryStream();
            //triples.CopyTo(memoryStream);
            //Assert.IsTrue(0 < memoryStream.Length);

            // do query
            var result = bc.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource13> ?p ?o }");

            var doc = XDocument.Load(result);
            var resultRows = doc.SparqlResultRows().ToArray();

            Assert.AreEqual(1, resultRows.Count());

            foreach (var row in resultRows)
            {
                var p = row.GetColumnValue("p");
                var o = row.GetColumnValue("o");

                Assert.AreEqual("http://example.org/property", p.ToString());
                Assert.AreEqual("http://example.org/resource2", o.ToString());
                Assert.IsNull(row.GetColumnValue("z"));
                Assert.IsFalse(row.IsLiteral("p"));
                Assert.IsFalse(row.IsLiteral("o"));
            }

            result = bc.ExecuteQuery(storeName, "select ?p ?o where { <http://example.org/resource14> ?p ?o }");
            doc = XDocument.Load(result);
            resultRows = doc.SparqlResultRows().ToArray();

            Assert.AreEqual(1, resultRows.Count());

            foreach (var row in resultRows)
            {
                var p = row.GetColumnValue("p");
                var o = row.GetColumnValue("o");

                Assert.AreEqual("http://example.org/property1", p.ToString());
                Assert.AreEqual(30, o);
                Assert.AreEqual("http://www.w3.org/2001/XMLSchema#integer", row.GetLiteralDatatype("o"));
                Assert.IsNull(row.GetLiteralDatatype("p"));
                Assert.IsNull(row.GetColumnValue("z"));
                Assert.IsFalse(row.IsLiteral("p"));
                Assert.IsTrue(row.IsLiteral("o"));
                Assert.IsInstanceOf(typeof(Int32), o);
            }
        }

        [Test]
        public void TestPassingNullForData()
        {
            var bc = GetClient();
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            const string triplesToAdd = @"<http://example.org/resource13> <http://example.org/property> <http://example.org/resource2>.";

            var jobInfo = bc.ExecuteTransaction(storeName,
                                                new UpdateTransactionData
                                                    {
                                                        ExistencePreconditions = "",
                                                        DeletePatterns = null,
                                                        InsertData = triplesToAdd
                                                    });

            Assert.IsNotNull(jobInfo);

            while (!jobInfo.JobCompletedOk && !jobInfo.JobCompletedWithErrors)
            {
                Thread.Sleep(50);
                jobInfo = bc.GetJobInfo(storeName, jobInfo.JobId);
            }

            //var triples = bc.GetStoreData(storeName);
            //var memoryStream = new MemoryStream();
            //triples.CopyTo(memoryStream);
            //memoryStream.Flush();
            //Assert.IsTrue(0 < memoryStream.Length);
        }

        [Test]
        public void TestEmbeddedClient()
        {
            var storeName = Guid.NewGuid().ToString();
            var client =
                BrightstarService.GetClient("type=embedded;storesDirectory=c:\\brightstar;storeName=" + storeName);

            const string tripleData = "<http://www.networkedplanet.com/people/gra> <http://www.networkedplanet.com/type/worksfor> <http://www.networkedplanet.com/companies/networkedplanet> .";
            client.CreateStore(storeName);
            client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData =  tripleData});
        }


        [Test]
        public void TestEmbeddedClientDeleteCreatePattern()
        {
            var storeName = "TestEmbeddedClientDeleteCreatePattern" + DateTime.Now.Ticks;

            // create store
            var client = BrightstarService.GetClient("type=embedded;storesDirectory=c:\\brightstar");
            client.CreateStore(storeName);

            if (client.DoesStoreExist(storeName))
            {
                // delete
                client.DeleteStore(storeName);

                //recreate
                client.CreateStore(storeName);
            }

            const string tripleData = "<http://www.networkedplanet.com/people/gra> <http://www.networkedplanet.com/type/worksfor> <http://www.networkedplanet.com/companies/networkedplanet> .";

            client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = tripleData});
        }


        [Test]
        public void TestExportWhileWriting()
        {
            const int firstBatchSize = 50000;
            var storeName = Guid.NewGuid().ToString();
            var client = GetClient();
            client.CreateStore(storeName);
            var batch1 = MakeTriples(0, firstBatchSize);
            var batch2 = MakeTriples(firstBatchSize, firstBatchSize+1000);
            var batch3 = MakeTriples(firstBatchSize+1000, firstBatchSize+2000);
            var batch4 = MakeTriples(firstBatchSize+2000, firstBatchSize+3000);

            // Verify batch size
            var p = new NTriplesParser();
            var counterSink = new CounterTripleSink();
            p.Parse(new StringReader(batch1), counterSink, Constants.DefaultGraphUri);
            Assert.AreEqual(firstBatchSize, counterSink.Count);

            var jobInfo = client.ExecuteTransaction(storeName, new UpdateTransactionData {InsertData = batch1});
            Assert.AreEqual(true, jobInfo.JobCompletedOk);

            // Second export with parallel store writes
            var exportJobInfo = client.StartExport(storeName, storeName + "_export.nt", label:"Export Data");
            Assert.That(exportJobInfo.Label, Is.EqualTo("Export Data"));
            jobInfo = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = batch2});
            Assert.AreEqual(true, jobInfo.JobCompletedOk);
            exportJobInfo = client.GetJobInfo(storeName, exportJobInfo.JobId);
            if (exportJobInfo.JobCompletedWithErrors)
            {
                Assert.Fail("Export job completed with errors: {0} : {1}", exportJobInfo.StatusMessage, exportJobInfo.ExceptionInfo);
            }
            if (exportJobInfo.JobCompletedOk)
            {
                Assert.Inconclusive("Export job completed before end of first concurrent import job.");
            }
            Assert.That(exportJobInfo.Label, Is.EqualTo("Export Data"));
            jobInfo = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData= batch3});
            Assert.AreEqual(true, jobInfo.JobCompletedOk);
            jobInfo = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = batch4});
            Assert.AreEqual(true, jobInfo.JobCompletedOk);
            while (!exportJobInfo.JobCompletedOk)
            {
                Assert.IsFalse(exportJobInfo.JobCompletedWithErrors);
                Thread.Sleep(1000);
                exportJobInfo = client.GetJobInfo(storeName, exportJobInfo.JobId);
            }

            var exportFile = new FileInfo(Path.Combine(Configuration.StoreLocation, "import", storeName + "_export.nt"));
            Assert.IsTrue(exportFile.Exists);
            var lineCount = File.ReadAllLines(exportFile.FullName).Count(x => !String.IsNullOrEmpty(x));
            Assert.AreEqual(firstBatchSize, lineCount);
        }

        public class CounterTripleSink : ITripleSink
        {
            public int Count { get; private set; }

            #region Implementation of ITripleSink

            /// <summary>
            /// Handler method for an individual RDF statement
            /// </summary>
            /// <param name="subject">The statement subject resource URI</param>
            /// <param name="subjectIsBNode">True if the value of <paramref name="subject"/> is a BNode identifier</param>
            /// <param name="predicate">The predicate resource URI</param>
            /// <param name="predicateIsBNode">True if the value of <paramref name="predicate"/> is a BNode identifier.</param>
            /// <param name="obj">The object of the statement</param>
            /// <param name="objIsBNode">True if the value of <paramref name="obj"/> is a BNode identifier.</param>
            /// <param name="objIsLiteral">True if the value of <paramref name="obj"/> is a literal string</param>
            /// <param name="dataType">The datatype URI for the object literal or null if the object is not a literal</param>
            /// <param name="langCode">The language code for the object literal or null if the object is not a literal</param>
            /// <param name="graphUri">The graph URI for the statement</param>
            public void Triple(string subject, bool subjectIsBNode, string predicate, bool predicateIsBNode, string obj, bool objIsBNode, bool objIsLiteral, string dataType, string langCode, string graphUri)
            {
                Count++;
            }

            /// <summary>
            /// Method invoked to indicate that no more triples remain to be written to the sink.
            /// </summary>
            public void Close()
            {
                // No-op
            }

            #endregion
        }

        private static string MakeTriples(int startId, int endId)
        {
            var triples = new StringBuilder();
            for(var i = startId; i < endId; i++ )
            {
                triples.AppendFormat("<http://www.example.org/resource/{0}> <http://example.org/value> \"{0}\" .\n",i);
            }
            return triples.ToString();
        }

        [Test]
        public void TestSpecialCharsInIdentities()
        {
            var importDir = Path.Combine(Configuration.StoreLocation, "import");
            if (!Directory.Exists(importDir))
            {
                Directory.CreateDirectory(importDir);
            }
            var testTarget = new FileInfo(importDir + Path.DirectorySeparatorChar + "persondata_en_subset.nt");
            if (!testTarget.Exists)
            {
                var testSource = new FileInfo("persondata_en_subset.nt");
                if (!testSource.Exists)
                {
                    Assert.Inconclusive("Could not locate test source file {0}. Test will not run", testSource.FullName);
                    return;
                }
                testSource.CopyTo(importDir + Path.DirectorySeparatorChar + "persondata_en_subset.nt");
            }

            var bc = BrightstarService.GetClient("type=http;endpoint=http://localhost:8090/brightstar");
            var storeName = Guid.NewGuid().ToString();
            bc.CreateStore(storeName);
            var jobInfo = bc.StartImport(storeName, "persondata_en_subset.nt", null, label:"Import Persondata");
            Assert.That(jobInfo.Label, Is.EqualTo("Import Persondata"));
            while (!(jobInfo.JobCompletedOk || jobInfo.JobCompletedWithErrors))
            {
                Thread.Sleep(1000);
                jobInfo = bc.GetJobInfo(storeName, jobInfo.JobId);
            }
            Assert.IsTrue(jobInfo.JobCompletedOk, "Import job failed");
            Assert.That(jobInfo.Label, Is.EqualTo("Import Persondata"));

            IDataObjectContext context = new EmbeddedDataObjectContext(new ConnectionString("type=embedded;storesDirectory=" + Configuration.StoreLocation + "\\"));
            var store = context.OpenStore(storeName);

            var test = store.BindDataObjectsWithSparql("SELECT ?p WHERE {?p a <http://xmlns.com/foaf/0.1/Person>} LIMIT 30").ToList();
            Assert.IsNotNull(test);

            foreach (var testDo in test)
            {
                Assert.IsNotNull(testDo);

                var propValues = testDo.GetPropertyValues("http://xmlns.com/foaf/0.1/name").OfType<PlainLiteral>();
                Assert.IsNotNull(propValues);
                Assert.IsTrue(propValues.Any());
            }
        }

        [Test]
        public void TestConsolidateEmptyStore()
        {
            var storeName = "ConsolidateEmptyStore_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);
            var job = client.ConsolidateStore(storeName);
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk, "Job did not complete successfully: {0} : {1}", job.StatusMessage, job.ExceptionInfo);
        }

        private static IJobInfo WaitForJob(IJobInfo job, IBrightstarService client, string storeName)
        {
            var cycleCount = 0;
            while (!job.JobCompletedOk && !job.JobCompletedWithErrors && cycleCount < 100)
            {
                Thread.Sleep(500);
                cycleCount++;
                job = client.GetJobInfo(storeName, job.JobId);
            }
            if (!job.JobCompletedOk && !job.JobCompletedWithErrors)
            {
                Assert.Fail("Job did not complete in time.");
            }
            return job;
        }

        [Test]
        public void TestConsolidatePopulatedStore()
        {
            var storeName = "ConsolidatePopulatedStore_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);
            const string addSet1 = "<http://example.org/people/alice> <http://www.w3.org/2000/01/rdf-schema#label> \"Alice\".";
            const string addSet2 = "<http://example.org/people/bob> <http://www.w3.org/2000/01/rdf-schema#label> \"Bob\".";
            const string addSet3 = "<http://example.org/people/carol> <http://www.w3.org/2000/01/rdf-schema#label> \"Carol\".";
            var result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = addSet1});
            Assert.IsTrue(result.JobCompletedOk);
            result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = addSet2});
            Assert.IsTrue(result.JobCompletedOk);
            result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = addSet3});
            Assert.IsTrue(result.JobCompletedOk);

            var job = client.ConsolidateStore(storeName, "Consolidate Store");
            Assert.IsNotNull(job);
            Assert.That(job.Label, Is.EqualTo("Consolidate Store"));
            var cycleCount = 0;
            while (!job.JobCompletedOk && !job.JobCompletedWithErrors && cycleCount < 100)
            {
                Thread.Sleep(500);
                cycleCount++;
                job = client.GetJobInfo(storeName, job.JobId);
                Assert.That(job.Label, Is.EqualTo("Consolidate Store"));
            }
            if (!job.JobCompletedOk && !job.JobCompletedWithErrors)
            {
                Assert.Fail("Job did not complete in time.");
            }
            Assert.IsTrue(job.JobCompletedOk, "Job did not complete successfully: {0} : {1}", job.StatusMessage, job.ExceptionInfo);

        }

        [Test]
        public void TestConsolidatePopulatedStoreAfterQuery()
        {
            var storeName = "ConsolidatePopulatedStore_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);
            const string addSet1 = "<http://example.org/people/alice> <http://www.w3.org/2000/01/rdf-schema#label> \"Alice\".";
            const string addSet2 = "<http://example.org/people/bob> <http://www.w3.org/2000/01/rdf-schema#label> \"Bob\".";
            const string addSet3 = "<http://example.org/people/carol> <http://www.w3.org/2000/01/rdf-schema#label> \"Carol\".";
            var result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = addSet1});
            Assert.IsTrue(result.JobCompletedOk);
            result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData = addSet2});
            Assert.IsTrue(result.JobCompletedOk);
            result = client.ExecuteTransaction(storeName, new UpdateTransactionData {InsertData = addSet3});
            Assert.IsTrue(result.JobCompletedOk);

            var resultsStream = client.ExecuteQuery(storeName, "SELECT * WHERE {?s ?p ?o}");
            resultsStream.Close();
            
            var job = client.ConsolidateStore(storeName);
            var cycleCount = 0;
            while (!job.JobCompletedOk && !job.JobCompletedWithErrors && cycleCount < 100)
            {
                Thread.Sleep(500);
                cycleCount++;
                job = client.GetJobInfo(storeName, job.JobId);
            }
            if (!job.JobCompletedOk && !job.JobCompletedWithErrors)
            {
                Assert.Fail("Job did not complete in time.");
            }
            Assert.IsTrue(job.JobCompletedOk, "Job did not complete successfully: {0} : {1}", job.StatusMessage, job.ExceptionInfo);

        }

        [Test]
        public void TestInsertQuadsIntoDefaultGraph()
        {
            var client = GetClient();
            var storeName = "QuadsTransaction1_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            const string txn1Adds =
                @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"" <http://example.org/graphs/alice> .
<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob"" .";
            var result = client.ExecuteTransaction(storeName, new UpdateTransactionData {InsertData = txn1Adds});
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInDefaultGraph(client, storeName, @"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob""");
            AssertTriplePatternInGraph(client, storeName, @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice""",
                "http://example.org/graphs/alice");
        }

        [Test]
        public void TestInsertQuadsIntoNonDefaultGraph()
        {
            var client = GetClient();
            var storeName = "QuadsTransaction2_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            const string txn1Adds =
    @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"" <http://example.org/graphs/alice> .
<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob"" .";
            var result = client.ExecuteTransaction(storeName,
                                                   new UpdateTransactionData
                                                       {
                                                           InsertData = txn1Adds,
                                                           DefaultGraphUri = "http://example.org/graphs/bob"
                                                       });
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInGraph(client, storeName, @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice""",
                "http://example.org/graphs/alice");
            AssertTriplePatternInGraph(client, storeName, @"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob""",
                "http://example.org/graphs/bob");
        }

        [Test]
        public void TestUpdateQuadsUsingDefaultGraph()
        {
            var client = GetClient();
            var storeName = "QuadsTransaction3_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            var txn1Adds = new StringBuilder();
            txn1Adds.AppendLine(
                @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"" <http://example.org/graphs/alice> .");
            txn1Adds.AppendLine(@"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob"" .");
            var result = client.ExecuteTransaction(storeName,
                                                   new UpdateTransactionData {InsertData = txn1Adds.ToString()});
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInDefaultGraph(client, storeName, @"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob""");
            AssertTriplePatternInGraph(client, storeName, @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice""",
                "http://example.org/graphs/alice");

            var txn2Adds = new StringBuilder();
            txn2Adds.AppendLine(@"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice Arnold"" <http://example.org/graphs/alice> .");
            txn2Adds.AppendLine(@"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob Bobbins"" .");

            result = client.ExecuteTransaction(storeName, new UpdateTransactionData
                {
                    ExistencePreconditions = txn1Adds.ToString(),
                    DeletePatterns = txn1Adds.ToString(),
                    InsertData = txn2Adds.ToString()
                });
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInGraph(client, storeName,
                                       @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice Arnold""",
                                       "http://example.org/graphs/alice");
            AssertTriplePatternInDefaultGraph(client, storeName,
                                       @"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob Bobbins""");
        }

        [Test]
        public void TestUpdateQuadsUsingNonDefaultGraph()
        {
            var client = GetClient();
            var storeName = "QuadsTransaction4_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            const string txn1Adds =
                @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"" <http://example.org/graphs/alice> .
<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob"" .";
            var result = client.ExecuteTransaction(storeName,
                                                   new UpdateTransactionData
                                                       {
                                                           InsertData = txn1Adds,
                                                           DefaultGraphUri = "http://example.org/graphs/bob"
                                                       });
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInGraph(client, storeName,
                                       @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice""",
                                       "http://example.org/graphs/alice");
            AssertTriplePatternInGraph(client, storeName,
                                       @"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob""",
                                       "http://example.org/graphs/bob");

            const string txn2Adds =
                @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice Arnold"" <http://example.org/graphs/alice> .
<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob Bobbins"" .";

            result = client.ExecuteTransaction(storeName, new UpdateTransactionData
                {
                    ExistencePreconditions = txn1Adds,
                    DeletePatterns = txn1Adds,
                    InsertData = txn2Adds,
                    DefaultGraphUri = "http://example.org/graphs/bob"
                });
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInGraph(client, storeName,
                                       @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice Arnold""",
                                       "http://example.org/graphs/alice");
            AssertTriplePatternInGraph(client, storeName,
                                       @"<http://example.org/people/bob> <http://xmlns.com/foaf/0.1/name> ""Bob Bobbins""",
                                       "http://example.org/graphs/bob");

        }


        [Test]
        public void TestTransactionWithWildcardGraph()
        {
            var client = GetClient();
            var storeName = "QuadsTransaction5_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            var txn1Adds = new StringBuilder();
            txn1Adds.AppendLine(@"<http://example.org/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"" <http://example.org/graphs/alice> .");
            txn1Adds.AppendLine(
                @"<http://example.org/alice> <http://xmlns.com/foaf/0.1/mbox> ""alice@example.org"" <http://example.org/graphs/alice> .");
            txn1Adds.AppendLine(@"<http://example.org/bob> <http://xmlns.com/foaf/0.1/name> ""Bob"" .");
            txn1Adds.AppendLine(@"<http://example.org/bob> <http://xmlns.com/foaf/0.1/mbox> ""bob@example.org"" .");

            var result = client.ExecuteTransaction(storeName,
                                                   new UpdateTransactionData {InsertData = txn1Adds.ToString()});
            Assert.IsTrue(result.JobCompletedOk);

            AssertTriplePatternInGraph(client, storeName,
                                       @"<http://example.org/alice> <http://xmlns.com/foaf/0.1/name> ""Alice""",
                                       "http://example.org/graphs/alice");
            AssertTriplePatternInDefaultGraph(client, storeName,
                                       @"<http://example.org/bob> <http://xmlns.com/foaf/0.1/name> ""Bob""");

            var txn2Deletes = new StringBuilder();
            txn2Deletes.AppendFormat(@"<{0}> <http://xmlns.com/foaf/0.1/name> <{0}> <{0}> .", Constants.WildcardUri);
            client.ExecuteTransaction(storeName, new UpdateTransactionData{DeletePatterns = txn2Deletes.ToString()});

            AssertTriplePatternNotInGraph(client, storeName,
                                       @"<http://example.org/alice> <http://xmlns.com/foaf/0.1/name> ""Alice""",
                                       "http://example.org/graphs/alice");
            AssertTriplePatternNotInDefaultGraph(client, storeName,
                                       @"<http://example.org/bob> <http://xmlns.com/foaf/0.1/name> ""Bob""");
            
        }

        [Test]
        public void TestGenerateAndRetrieveStats()
        {
            var client = GetClient();
            var storeName = "GenerateAndRetrieveStats_" + DateTime.Now.Ticks;
            client.CreateStore(storeName);

            var txn1Adds = new StringBuilder();
            txn1Adds.AppendLine(@"<http://example.org/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"" <http://example.org/graphs/alice> .");
            txn1Adds.AppendLine(
                @"<http://example.org/alice> <http://xmlns.com/foaf/0.1/mbox> ""alice@example.org"" <http://example.org/graphs/alice> .");
            txn1Adds.AppendLine(@"<http://example.org/bob> <http://xmlns.com/foaf/0.1/name> ""Bob"" .");
            txn1Adds.AppendLine(@"<http://example.org/bob> <http://xmlns.com/foaf/0.1/mbox> ""bob@example.org"" .");

            Thread.Sleep(1000);
            client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData=txn1Adds.ToString()});
            var commitId = client.GetCommitPoints(storeName, 0, 1).Select(s => s.Id).First();

            var stats = client.GetStatistics(storeName);
            Assert.IsNull(stats);

            var job = client.UpdateStatistics(storeName, "Update Store Statistics");
            Assert.That(job.Label, Is.EqualTo("Update Store Statistics"));
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);
            Assert.That(job.Label, Is.EqualTo("Update Store Statistics"));

            stats = client.GetStatistics(storeName);
            Assert.IsNotNull(stats);
            Assert.AreEqual(4, stats.TotalTripleCount);
            Assert.AreEqual(2, stats.PredicateTripleCounts.Count);
            
            Assert.AreEqual(commitId, stats.CommitId);
        }

        [Test]
        public void TestExecuteTransaction()
        {
            var client = GetClient();
            var storeName = "TestExecuteTransaction_" + DateTime.Now.Ticks;

            client.CreateStore(storeName);

            // Test a simple addition of triples
            var insertData = new StringBuilder();
            insertData.AppendLine(@"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/name> ""Alice"".");
            insertData.AppendLine(
                @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/mbox> ""alice@example.org"".");
            var job = client.ExecuteTransaction(storeName,
                                                new UpdateTransactionData { InsertData = insertData.ToString() });
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);

            //var resultStream = client.ExecuteQuery(storeName, "SELECT * WHERE { ?s <http://xmlns.com/foaf/0.1/mbox> ?p }");
            //var resultDoc = XDocument.Load(resultStream);
            //var resultRows = resultDoc.SparqlResultRows().ToList();
            //Assert.AreEqual(1, resultRows.Count);

            // Test an update with a precondition which is met
            const string tripleToDelete = @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/mbox> ""alice@example.org"".";
            const string tripleToInsert = @"<http://example.org/people/alice> <http://xmlns.com/foaf/0.1/mbox_sha1sum> ""FAKESHA1""";
            job = client.ExecuteTransaction(storeName,
                                            new UpdateTransactionData
                                            {
                                                ExistencePreconditions = tripleToDelete,
                                                DeletePatterns = tripleToDelete,
                                                InsertData = tripleToInsert
                                            });
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);

            // Test an update with a precondition which is not met
            job = client.ExecuteTransaction(storeName,
                                            new UpdateTransactionData
                                            {
                                                ExistencePreconditions = tripleToDelete,
                                                DeletePatterns = tripleToDelete,
                                                InsertData = tripleToInsert
                                            });
            WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedWithErrors);
        }

        [Test]
        public void TestCreateSnapshot()
        {
            var storeName = "CreateSnapshot_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);
            const string addSet1 = "<http://example.org/people/alice> <http://www.w3.org/2000/01/rdf-schema#label> \"Alice\".";
            const string addSet2 = "<http://example.org/people/bob> <http://www.w3.org/2000/01/rdf-schema#label> \"Bob\".";
            const string addSet3 = "<http://example.org/people/carol> <http://www.w3.org/2000/01/rdf-schema#label> \"Carol\".";
            var result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData=addSet1}, waitForCompletion:true);
            Assert.IsTrue(result.JobCompletedOk);
            result = client.ExecuteTransaction(storeName, new UpdateTransactionData{InsertData=addSet2}, waitForCompletion:true);
            Assert.IsTrue(result.JobCompletedOk);
            result = client.ExecuteTransaction(storeName, new UpdateTransactionData { InsertData = addSet3 }, waitForCompletion: true);
            Assert.IsTrue(result.JobCompletedOk);

            var resultsStream = client.ExecuteQuery(storeName, "SELECT * WHERE {?s ?p ?o}");
            resultsStream.Close();

            var commitPoints = client.GetCommitPoints(storeName, 0, 2).ToList();
            Assert.AreEqual(2, commitPoints.Count);

            // Append Only targets
            // Create from default (latest) commit
            var job = client.CreateSnapshot(storeName, storeName + "_snapshot1", PersistenceType.AppendOnly, label:"Snapshot Store");
            Assert.That(job, Is.Not.Null);
            Assert.That(job.Label, Is.EqualTo("Snapshot Store"));
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);
            Assert.That(job.Label, Is.EqualTo("Snapshot Store"));
            resultsStream = client.ExecuteQuery(storeName + "_snapshot1", "SELECT * WHERE { ?s ?p ?o }");
            var resultsDoc = XDocument.Load(resultsStream);
            Assert.AreEqual(3, resultsDoc.SparqlResultRows().Count());
            // Create from specific commit point
            job = client.CreateSnapshot(storeName, storeName + "_snapshot2", PersistenceType.AppendOnly, commitPoints[1]);
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);
            resultsStream = client.ExecuteQuery(storeName + "_snapshot2", "SELECT * WHERE {?s ?p ?o}");
            resultsDoc = XDocument.Load(resultsStream);
            Assert.AreEqual(2, resultsDoc.SparqlResultRows().Count());

            // Rewrite targets
            // Create from default (latest) commit
            job = client.CreateSnapshot(storeName, storeName + "_snapshot3", PersistenceType.Rewrite);
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);
            resultsStream = client.ExecuteQuery(storeName + "_snapshot3", "SELECT * WHERE { ?s ?p ?o }");
            resultsDoc = XDocument.Load(resultsStream);
            Assert.AreEqual(3, resultsDoc.SparqlResultRows().Count());
            // Create from specific commit point
            job = client.CreateSnapshot(storeName, storeName + "_snapshot4", PersistenceType.Rewrite, commitPoints[1]);
            job = WaitForJob(job, client, storeName);
            Assert.IsTrue(job.JobCompletedOk);
            resultsStream = client.ExecuteQuery(storeName + "_snapshot4", "SELECT * WHERE {?s ?p ?o}");
            resultsDoc = XDocument.Load(resultsStream);
            Assert.AreEqual(2, resultsDoc.SparqlResultRows().Count());

        }

        [Test]
        public void TestListJobs()
        {
            var storeName = "TestListJobs_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);

            var jobs = client.GetJobInfo(storeName, 0, 10).ToList();
            Assert.That(jobs.Count == 0);

            var job = client.UpdateStatistics(storeName);
            job = WaitForJob(job, client, storeName);
            Assert.That(job.JobCompletedOk);

            jobs = client.GetJobInfo(storeName, 0, 10).ToList();
            Assert.That(jobs.Count == 1);
            Assert.That(jobs[0].JobId == job.JobId);

            var job2 = client.ExecuteTransaction(storeName,
                                                 new UpdateTransactionData
                                                     {
                                                         InsertData =
                                                             "<http://example.org/s> <http://example.org/p> <http://example.org/o> ."
                                                     });
            job2 = WaitForJob(job2, client, storeName);
            Assert.That(job.JobCompletedOk);

            jobs = client.GetJobInfo(storeName, 0, 10).ToList();
            Assert.That(jobs.Count, Is.EqualTo(2));
            Assert.That(jobs[0].JobId, Is.EqualTo(job2.JobId));
            Assert.That(jobs[1].JobId, Is.EqualTo(job.JobId));

            jobs = client.GetJobInfo(storeName, 0, 1).ToList();
            Assert.That(jobs.Count, Is.EqualTo(1));
            Assert.That(jobs[0].JobId, Is.EqualTo(job2.JobId));

            jobs = client.GetJobInfo(storeName, 1, 1).ToList();
            Assert.That(jobs.Count, Is.EqualTo(1));
            Assert.That(jobs[0].JobId, Is.EqualTo(job.JobId));
        }

        [Test]
        public void TestJobLabel()
        {
            var storeName = "TestJobLabel_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);

            var jobs = client.GetJobInfo(storeName, 0, 10).ToList();
            Assert.That(jobs.Count(), Is.EqualTo(0));

            var updateStats = client.UpdateStatistics(storeName, "Statistics Update");
            Assert.That(updateStats.Label, Is.EqualTo("Statistics Update"));
            jobs = client.GetJobInfo(storeName, 0, 10).ToList();
            Assert.That(jobs.Count, Is.EqualTo(1));
            Assert.That(jobs[0].JobId, Is.EqualTo(updateStats.JobId));
            Assert.That(jobs[0].Label, Is.EqualTo("Statistics Update"));

        }

        [Test]
        public void TestGetJobInfoParameterValidation()
        {
            var storeName = "TestGetJobInfoParameterValidation_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);

            try
            {
                client.GetJobInfo(storeName, -1, 10);
                Assert.Fail("Expected ArgumentException when skip < 0");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("skip"));
            }

            try
            {
                client.GetJobInfo(storeName, 0, 0);
                Assert.Fail("Expected ArgumentException when take == 0");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("take"));
            }

            try
            {
                client.GetJobInfo(storeName, 0, -1);
                Assert.Fail("Expected ArgumentException when take < 0");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("take"));
            }

            try
            {
                client.GetJobInfo(null, 10, 10);
                Assert.Fail("Expected ArgumentNullException when storeName is NULL");
            }
            catch (ArgumentNullException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("storeName"));
            }

            try
            {
                client.GetJobInfo("Invalid" + storeName, 0, 10);
                Assert.Fail("Expected BrightstarClientException when store does not exist");
            } catch(BrightstarClientException){}

        }

        [Test]
        public void TestRdfImportFormatOverride()
        {
            CopyTestDataToImportFolder("simple.txt", "simple.rdf");
            var storeName = "TestRdfImportFormatOverride_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);
            var importJob = client.StartImport(storeName, "simple.rdf", importFormat:RdfFormat.NTriples);
            importJob = WaitForJob(importJob, client, storeName);
            Assert.That(importJob.JobCompletedOk, "Import failed: {0} - {1}", importJob.StatusMessage, importJob.ExceptionInfo);
        }

        [Test]
        public void TestRdfXmlExport()
        {
            CopyTestDataToImportFolder("simple.txt");
            var storeName = "TestRdfXmlExport_" + DateTime.Now.Ticks;
            var client = GetClient();
            client.CreateStore(storeName);

            var importJob = client.StartImport(storeName, "simple.txt");
            importJob = WaitForJob(importJob, client, storeName);
            Assert.That(importJob.JobCompletedOk, "Import failed: {0} - {1}", importJob.StatusMessage, importJob.ExceptionInfo);
            var pathToExport = Path.Combine(Configuration.StoreLocation, "import", "simple.rdf");
            if (File.Exists(pathToExport)) File.Delete(pathToExport);

            var exportJob = client.StartExport(storeName, "simple.rdf", exportFormat: RdfFormat.RdfXml,
                                               graphUri: Constants.DefaultGraphUri);
            exportJob = WaitForJob(exportJob, client, storeName);
            Assert.That(exportJob.JobCompletedOk, "Export failed: {0} - {1}", exportJob.StatusMessage, exportJob.ExceptionInfo);
            Assert.That(File.Exists(pathToExport));

            var g = new Graph();
            var parser = new RdfXmlParser();
            parser.Load(g, pathToExport);

            // TODO: Validate expected content

        }

        [Test]
        public void TestCreateEntityWithNoContext()
        {
            MyEntityContext.InitializeEntityMappingStore();
            var entity = new BaseEntity {Id = "foo"};
        }
    }
}

#endif