USE [OQMS]
GO
/****** Object:  Table [dbo].[RuleAuditLogs]    Script Date: 02-06-2026 16:40:54 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RuleAuditLogs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[EntityType] [nvarchar](100) NOT NULL,
	[EntityName] [nvarchar](200) NOT NULL,
	[Action] [nvarchar](50) NOT NULL,
	[FieldName] [nvarchar](200) NULL,
	[OldValue] [nvarchar](max) NULL,
	[NewValue] [nvarchar](max) NULL,
	[ChangedBy] [nvarchar](200) NULL,
	[ChangedDate] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_RuleAuditLogs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WorkflowRules]    Script Date: 02-06-2026 16:40:56 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WorkflowRules](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[WorkflowId] [int] NOT NULL,
	[RuleName] [nvarchar](200) NOT NULL,
	[Enabled] [bit] NOT NULL,
	[SampleJson] [nvarchar](max) NULL,
	[Definition] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_WorkflowRules] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Workflows]    Script Date: 02-06-2026 16:40:56 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Workflows](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[WorkflowName] [nvarchar](200) NOT NULL,
	[GlobalParamsJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_Workflows] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
ALTER TABLE [dbo].[RuleAuditLogs] ADD  DEFAULT (sysutcdatetime()) FOR [ChangedDate]
GO
ALTER TABLE [dbo].[WorkflowRules] ADD  DEFAULT ((1)) FOR [Enabled]
GO
ALTER TABLE [dbo].[WorkflowRules]  WITH CHECK ADD  CONSTRAINT [FK_WorkflowRules_Workflows_WorkflowId] FOREIGN KEY([WorkflowId])
REFERENCES [dbo].[Workflows] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[WorkflowRules] CHECK CONSTRAINT [FK_WorkflowRules_Workflows_WorkflowId]
GO
