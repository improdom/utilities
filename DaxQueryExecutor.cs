using System;

namespace YourNamespace.Models
{
    public class QuerySpaceExecutionStatus
    {
        public long QuerySpaceId { get; set; }

        public DateTime CobDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime? LastAttempt { get; set; }

        public DateTime LastUpdated { get; set; }

        public bool ReadyEventPublished { get; set; }
    }
}



using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class QuerySpaceExecutionStatusConfig : IEntityTypeConfiguration<QuerySpaceExecutionStatus>
{
    public void Configure(EntityTypeBuilder<QuerySpaceExecutionStatus> builder)
    {
        builder.ToTable("pbi_query_space_execution_status", schema: "mr_agg_model_refresh");

        builder.HasKey(x => new { x.QuerySpaceId, x.CobDate });

        builder.Property(x => x.Status)
               .HasColumnName("status")
               .HasMaxLength(50);

        builder.Property(x => x.LastAttempt)
               .HasColumnName("last_attempt");

        builder.Property(x => x.LastUpdated)
               .HasColumnName("last_updated");

        builder.Property(x => x.ReadyEventPublished)
               .HasColumnName("ready_event_published");
    }
}
