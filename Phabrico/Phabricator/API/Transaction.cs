using Newtonsoft.Json.Linq;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represents some metadata for a Phabricator object.
    /// This can be comments, task state changes, ...
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// Returns all transactions for a given Phabricator object
        /// </summary>
        /// <param name="database"></param>
        /// <param name="conduit"></param>
        /// <param name="parentTokenId"></param>
        /// <returns></returns>
        public static IEnumerable<Data.Transaction> Get(Database database, Conduit conduit, string parentTokenId)
        {
            JObject transactionData = conduit.Query("transaction.search", new JObject
                    {
                        {"objectIdentifier", parentTokenId }
                    });

            List<JObject> transactions = transactionData["result"]["data"].OfType<JObject>().ToList();

            if (parentTokenId.StartsWith(Phabricator.Data.Phriction.Prefix))
            {
                foreach (JObject transaction in transactions)
                {
                    Data.Transaction newTransaction = new Data.Transaction();
                    newTransaction.Token = parentTokenId;
                    newTransaction.ID = transaction["id"].ToString();
                    newTransaction.Type = "";
                    newTransaction.Author = transaction["authorPHID"].ToString();
                    newTransaction.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(transaction["dateModified"].ToString()));

                    yield return newTransaction;
                }
            }

            if (parentTokenId.StartsWith(Phabricator.Data.Maniphest.Prefix))
            {
                foreach (JObject transaction in transactions)
                {
                    Data.Transaction newTransaction = new Data.Transaction();
                    newTransaction.Token = parentTokenId;
                    newTransaction.ID = transaction["id"].ToString();
                    newTransaction.Type = transaction["type"].ToString();
                    newTransaction.Author = transaction["authorPHID"].ToString();
                    newTransaction.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(transaction["dateModified"].ToString()));

                    switch (newTransaction.Type.ToLower())
                    {
                        case "comment":
                            if (transaction["comments"][0]["removed"].ToObject<bool>())
                            {
                                newTransaction.OldValue = "";
                                newTransaction.NewValue = "";
                            }
                            else
                            {
                                newTransaction.OldValue = "";
                                if (transaction["comments"].Count() > 1)
                                {
                                    newTransaction.OldValue = transaction["comments"][1]["content"]["raw"].ToString();
                                }
                                newTransaction.NewValue = transaction["comments"][0]["content"]["raw"].ToString();
                            }
                            break;

                        case "owner":
                        case "status":
                            newTransaction.OldValue = transaction["fields"]["old"].ToString();
                            newTransaction.NewValue = transaction["fields"]["new"].ToString();
                            break;

                        case "priority":
                            newTransaction.OldValue = transaction["fields"]["old"]["value"].ToString();
                            newTransaction.NewValue = transaction["fields"]["new"]["value"].ToString();
                            break;

                        case "title":
                        case "description":
                        case "projects":
                        case "subscribers":
                            // ignore details of these changes but still keep this type to find out who did the change (it might be the 'LastModifiedBy' value)
                            break;

                        default:
                            // ignore changes of other types because they are not interesting and cause too much bloat for the local sqlite database
                            continue;
                    }

                    yield return newTransaction;
                }
            }
        }
    }
}
