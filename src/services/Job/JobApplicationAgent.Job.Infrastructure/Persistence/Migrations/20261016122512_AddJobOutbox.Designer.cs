using JobApplicationAgent.Job.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Job.Infrastructure.Persistence.Migrations;

[DbContext(typeof(JobDbContext))]
[Migration("20261016122512_AddJobOutbox")]
partial class AddJobOutbox { }
