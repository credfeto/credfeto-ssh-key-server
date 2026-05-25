using System;
using System.Diagnostics;

namespace Credfeto.Keys.Server.Models;

[DebuggerDisplay("KeyId: {KeyId}")]
internal readonly record struct AddKeyResponse(Guid KeyId);
