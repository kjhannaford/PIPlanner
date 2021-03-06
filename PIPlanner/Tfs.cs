﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using System.Text;
using System.Configuration;

namespace PIPlanner
{
    public class Tfs
    {
        private readonly WorkItemStore store;

        public Tfs(Uri tfsUri)
        {
            TfsConfigurationServer configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(tfsUri);
            // Get the catalog of team project collections
            ReadOnlyCollection<CatalogNode> collectionNodes = configurationServer.CatalogNode.QueryChildren(new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

            // List the team project collections
            foreach (CatalogNode collectionNode in collectionNodes)
            {
                // Use the InstanceId property to get the team project collection
                Guid collectionId = new Guid(collectionNode.Resource.Properties["InstanceId"]);
                TfsTeamProjectCollection tfs = configurationServer.GetTeamProjectCollection(collectionId);
                store = (WorkItemStore) tfs.GetService(typeof (WorkItemStore));
            }
        }

        public ICollection<string> Projects
        {
            get { return (from Project proj in store.Projects select proj.Name).ToList(); }
        }

        public ICollection<Iteration> GetIterationPaths(string projectName)
        {
            var result = new List<Iteration>();

            foreach (Project project in store.Projects.Cast<Project>().
                Where(project => string.Compare(project.Name, projectName, StringComparison.OrdinalIgnoreCase) == 0))
            {
                foreach (Node node in project.IterationRootNodes)
                {
                    string path = project.Name + "\\" + node.Name;
                    result.Add(new Iteration(node.Id, path));
                    RecursiveAddIterationPath(node, result, path);
                }

                break;
            }

            return result;
        }

        private static void RecursiveAddIterationPath(Node node, ICollection<Iteration> result, string parentIterationName)
        {
            foreach (Node item in node.ChildNodes)
            {
                string path = parentIterationName + "\\" + item.Name;
                result.Add(new Iteration(item.Id, path));
                if (item.HasChildNodes)
                {
                    RecursiveAddIterationPath(item, result, path);
                }
            }
        }

        public ICollection<WorkItem> GetWorkItemsUnderIterationPath(string iterationPath)
        {
            ICollection<WorkItem> result = new Collection<WorkItem>();
            string query = GetWorkItemQuery(iterationPath, true);
            //string query = string.Format(CultureInfo.CurrentCulture,
            //                             "SELECT [System.Id], [System.IterationId], [System.IterationPath], [System.State], [System.Title] " +
            //                             "FROM WorkItems WHERE [Work Item Type] = 'User Story' AND [System.IterationPath] UNDER '{0}'",
            //                             iterationPath);      
            foreach (WorkItem item in store.Query(query))
            {
                result.Add(item);
            }

            return result;
        }

        public ICollection<WorkItem> GetWorkItemsInIterationPath(string iterationPath)
        {
            ICollection<WorkItem> result = new Collection<WorkItem>();
            string query = GetWorkItemQuery(iterationPath, false);
                //string.Format(CultureInfo.CurrentCulture,
            //                             "SELECT [System.Id], [System.IterationId], [System.IterationPath], [System.State], [System.Title] " +
            //                             "FROM WorkItems WHERE [Work Item Type] = 'User Story' AND [System.IterationPath] = '{0}'",
            //                             iterationPath);
            foreach (WorkItem item in store.Query(query))
            {
                result.Add(item);
                
            }
            

            return result;
        }

        public WorkItem GetWorkItem(string id)
        {
            string query = string.Format(CultureInfo.CurrentCulture,
                                         "SELECT [System.Id], [System.IterationId], [System.IterationPath], [System.State], [System.Title] " +
                                         "FROM WorkItems WHERE [System.Id] = {0}",
                                         id);
            foreach (WorkItem item in store.Query(query))
            {
                return item;
            }

            return null;
        }

        string GetWorkItemQuery(string iterationPath, bool under)
        {
            string extraFilter = "";
            if (ConfigurationManager.AppSettings["WorkItemQueryFilter"] != null)
            {
                extraFilter = ConfigurationManager.AppSettings["WorkItemQueryFilter"].ToString();
            }

            string query = string.Format(CultureInfo.CurrentCulture,
                                         "SELECT [System.Id], [System.IterationId], [System.IterationPath], [System.State], [System.Title] " +
                                         "FROM WorkItems WHERE [System.IterationPath] " + (under ? "UNDER" : "=") + " '{0}' " + extraFilter,
                                         iterationPath);

            return query;
        }

        public WorkItemLinkInfo[] GetDependentItemIds(int id)
        {
            StringBuilder queryString = new StringBuilder("SELECT *" +
                                                          " FROM WorkItemLinks " +
                                                          " WHERE [System.Links.LinkType] = 'System.LinkTypes.Dependency-Reverse' AND  [Source].[System.Id] = " + id
                                                          );
            Query wiQuery = new Query(store, queryString.ToString());
            WorkItemLinkInfo[] wiTrees = wiQuery.RunLinkQuery();

            return wiTrees;
        }
    }
}