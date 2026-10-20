using JobApplicationAgent.Collection.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Collection.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CollectionDbContext))]
[Migration("20261017170000_AddCollectionOutboxCorrelationId")]
partial class AddCollectionOutboxCorrelationId { }
