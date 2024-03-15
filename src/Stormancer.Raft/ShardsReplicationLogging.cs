using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    internal static partial class ShardsReplicationLogging
    {

        [LoggerMessage(Level = LogLevel.Trace, Message = "Starting processing command {commandId}")]
        public static partial void LogStartProcessingCommand(ILogger logger, Guid commandId);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Completed processing command {commandId}")]
        public static partial void LogCompleteProcessingCommand(ILogger logger, Guid commandId);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Sending append entry command from {leader} to {replica} term={currentTerm} {firstEntry}=>{lastEntry} | commit={commit}")]
        public static partial void LogSendingAppendCommand(ILogger logger,Guid leader, Guid replica, ulong currentTerm, ulong firstEntry, ulong lastEntry,ulong commit );


        [LoggerMessage(Level =LogLevel.Trace,Message = "Sending response to AppendEntries to {destination} : {success}, leaderUid={leader} term={term} lastEntry={lastEntry} applied={applied}")]
        public static partial void LogSendingAppendCommandResponse(ILogger logger, Guid destination, bool success, Guid? leader, ulong term, ulong lastEntry, ulong applied);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Sending request vote to {destination}. Current term {currentTerm}, lastEntry={lastLogEntry}:{lastLogEntryTerm}")]
        public static partial void SendingRequestVote(ILogger logger, Guid destination, ulong currentTerm, ulong lastLogEntry, ulong lastLogEntryTerm);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Sending request vote response from {origin} to {destination}. voteGranted {voteGranted} to {votedFor} Current term {currentTerm}, lastEntry={lastLogEntry}:{lastLogEntryTerm},")]
        public static partial void SendingRequestVoteResult(ILogger logger, Guid origin, Guid destination, bool voteGranted, Guid? votedFor, ulong currentTerm, ulong lastLogEntry, ulong lastLogEntryTerm);
    }
}
