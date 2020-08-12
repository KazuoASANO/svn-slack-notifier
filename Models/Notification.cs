﻿using System;

namespace SVNWebexNotifier.Models
{
    public class Notification
    {
        // Repository-specific
        public string RepositoryName { get; set; }
        public string RepositoryURL { get; set; }
        public string RepositoryPath { get; set; }
        public string Revision { get; set; }

        // Commit-specific
        public string CommitAuthor { get; set; }
        public string CommitMessage { get; set; }
        public string CommitChanged { get; set; }

        // Slack-specific
        public string Channel { get; set; }
    }
}
