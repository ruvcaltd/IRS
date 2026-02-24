using System;
using System.Collections.Generic;
using IRS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure.Data;

public partial class IrsDbContext : DbContext
{
    public IrsDbContext(DbContextOptions<IrsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Agent> Agents { get; set; }

    public virtual DbSet<AgentRun> AgentRuns { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<ResearchPage> ResearchPages { get; set; }

    public virtual DbSet<ResearchPageAgent> ResearchPageAgents { get; set; }

    public virtual DbSet<SectionAgent> SectionAgents { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Section> Sections { get; set; }

    public virtual DbSet<Security> Securities { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamMember> TeamMembers { get; set; }

    public virtual DbSet<TeamRole> TeamRoles { get; set; }

    public virtual DbSet<TeamSecret> TeamSecrets { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<LlmProvider> LlmProviders { get; set; }

    public virtual DbSet<LlmModel> LlmModels { get; set; }

    public virtual DbSet<GlobalLlmConfiguration> GlobalLlmConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Agents__3213E83F37C43DE4");

            entity.HasIndex(e => e.endpoint_url, "IX_Agents_EndpointUrl").HasFilter("([is_deleted]=(0))");

            entity.HasIndex(e => new { e.team_id, e.visibility }, "IX_Agents_Team_Visibility").HasFilter("([is_deleted]=(0))");

            entity.HasIndex(e => e.llm_model_id, "IX_Agents_LlmModelId").HasFilter("([is_deleted]=(0))");

            entity.Property(e => e.auth_type).HasDefaultValue("None");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.http_method).HasDefaultValue("GET");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.visibility).HasDefaultValue("Private");

            entity.HasOne(d => d.owner_user).WithMany(p => p.Agents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Agents__owner_us__59FA5E80");

            entity.HasOne(d => d.team).WithMany(p => p.Agents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Agents__team_id__59063A47");

            entity.HasOne(d => d.llm_model).WithMany(p => p.Agents)
                .HasConstraintName("FK_Agents_LlmModel");
        });

        modelBuilder.Entity<AgentRun>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__AgentRun__3213E83F5CEBA356");

            entity.HasIndex(e => e.section_agent_id, "IX_AgentRuns_SectionAgentId");

            entity.Property(e => e.started_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.research_page_agent).WithMany(p => p.AgentRuns).HasConstraintName("FK__AgentRuns__resea__7D439ABD");

            entity.HasOne(d => d.section_agent).WithMany(p => p.AgentRuns).HasConstraintName("FK_AgentRuns_SectionAgent");

            entity.HasOne(d => d.section).WithMany(p => p.AgentRuns).HasConstraintName("FK__AgentRuns__secti__7E37BEF6");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Comments__3213E83F316539DF");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.author).WithMany(p => p.Comments).HasConstraintName("FK__Comments__author__72C60C4A");

            entity.HasOne(d => d.section).WithMany(p => p.Comments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Comments__sectio__71D1E811");
        });

        modelBuilder.Entity<ResearchPage>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Research__3213E83FE9E410F4");

            entity.HasIndex(e => new { e.team_id, e.security_figi }, "IX_ResearchPages_Team_SecurityFigi").HasFilter("([is_deleted]=(0))");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.security_figiNavigation).WithMany(p => p.ResearchPages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ResearchP__secur__656C112C");

            entity.HasOne(d => d.team).WithMany(p => p.ResearchPages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ResearchP__team___6477ECF3");
        });

        modelBuilder.Entity<ResearchPageAgent>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Research__3213E83FCDC126FF");

            entity.Property(e => e.is_enabled).HasDefaultValue(true);

            entity.HasOne(d => d.agent).WithMany(p => p.ResearchPageAgents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ResearchP__agent__787EE5A0");

            entity.HasOne(d => d.research_page).WithMany(p => p.ResearchPageAgents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ResearchP__resea__778AC167");
        });

        modelBuilder.Entity<SectionAgent>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__SectionA__3213E83FCDC126FF");

            entity.Property(e => e.is_enabled).HasDefaultValue(true);

            entity.HasOne(d => d.agent).WithMany(p => p.SectionAgents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SectionA__agent__787EE5A0");

            entity.HasOne(d => d.section).WithMany(p => p.SectionAgents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SectionA__secti__778AC167");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Roles__3213E83F24DBBCCB");
        });

        modelBuilder.Entity<Section>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Sections__3213E83F02D840AA");

            entity.HasIndex(e => e.research_page_id, "IX_Sections_ResearchPageId").HasFilter("([is_deleted]=(0))");

            entity.HasOne(d => d.research_page).WithMany(p => p.Sections)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Sections__resear__6B24EA82");
        });

        modelBuilder.Entity<Security>(entity =>
        {
            entity.HasKey(e => e.figi).HasName("PK__Securiti__35718DE51D309C09");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Teams__3213E83F81E73382");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.team_id }).HasName("PK__TeamMemb__663CE9D41F5D9D47");

            entity.HasIndex(e => new { e.team_id, e.status }, "IX_TeamMembers_Team_Status").HasFilter("([is_deleted]=(0))");

            entity.HasIndex(e => new { e.team_id, e.status, e.team_role_id }, "IX_TeamMembers_Team_Status_Role").HasFilter("([is_deleted]=(0))");

            entity.HasIndex(e => new { e.user_id, e.status }, "IX_TeamMembers_User_Status").HasFilter("([is_deleted]=(0))");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.status).HasDefaultValue("PENDING");

            entity.HasOne(d => d.approved_by_user).WithMany(p => p.TeamMemberapproved_by_users).HasConstraintName("FK__TeamMembe__appro__4D94879B");

            entity.HasOne(d => d.team).WithMany(p => p.TeamMembers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__team___4BAC3F29");

            entity.HasOne(d => d.team_role).WithMany(p => p.TeamMembers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__team___4CA06362");

            entity.HasOne(d => d.user).WithMany(p => p.TeamMemberusers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__user___4AB81AF0");
        });

        modelBuilder.Entity<TeamRole>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__TeamRole__3213E83FDD8BF1E5");
        });

        modelBuilder.Entity<TeamSecret>(entity =>
        {
            entity.HasKey(e => new { e.team_id, e.key_name }).HasName("PK__TeamSecr__C06B8FA146FDFD0E");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.team).WithMany(p => p.TeamSecrets)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamSecre__team___52593CB8");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__Users__3213E83FA1C503C4");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__role_id__3D5E1FD2");
        });

        modelBuilder.Entity<LlmProvider>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__LlmProviders");

            entity.HasIndex(e => e.name, "UQ_LlmProviders_Name").IsUnique();

            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<LlmModel>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__LlmModels");

            entity.HasIndex(e => new { e.provider_id, e.model_identifier }, "UQ_LlmModels_ProviderModel").IsUnique();

            entity.HasIndex(e => e.provider_id, "IX_LlmModels_ProviderId").HasFilter("([is_active]=(1))");

            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.supports_streaming).HasDefaultValue(true);
            entity.Property(e => e.supports_function_calling).HasDefaultValue(true);
            entity.Property(e => e.supports_vision).HasDefaultValue(false);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.provider).WithMany(p => p.LlmModels)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LlmModels_Provider");
        });

        modelBuilder.Entity<GlobalLlmConfiguration>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__GlobalLlmConfiguration");

            entity.Property(e => e.id).HasDefaultValue(1);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.global_model).WithOne(p => p.GlobalLlmConfiguration)
                .HasConstraintName("FK_GlobalLlmConfiguration_Model");

            entity.HasOne(d => d.updated_by_user).WithMany(p => p.GlobalLlmConfigurations)
                .HasConstraintName("FK_GlobalLlmConfiguration_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
