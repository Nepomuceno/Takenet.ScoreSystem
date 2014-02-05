﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.DataServices;
using Microsoft.WindowsAzure.Storage.Table.Queryable;
using Takenet.ScoreSystem.Core;
using Takenet.ScoreSystem.Base.Repository;
using Takenet.ScoreSystem.Core.Exceptions;

namespace Takenet.ScoreSystem.Base
{
    
    public class ScoreSystemBase : IScoreSystem
    {
        private const int MAX_TRANSACTIONS = 15;
        private readonly IScoreSystemRepository _scoreSystemRepository;
        private Dictionary<byte, Dictionary<string,double>> _checkPatterns;

        public Dictionary<byte, Dictionary<string, double>> CheckPatterns
        {
            get
            {
                if (_checkPatterns == null)
                {
                    _checkPatterns = _scoreSystemRepository.FillPatterns();
                }
                return _checkPatterns;
            }
        }
        public ScoreSystemBase(IScoreSystemRepository scoreSystemRepository)
        {
            _scoreSystemRepository = scoreSystemRepository;
        }
        
        public async Task<Pattern> IncludeOrChangePattern(string pattern, double value, byte historySize)
        {
            var resultPattern = new Pattern(pattern, value, historySize);
            await _scoreSystemRepository.IncludeOrChangePattern(resultPattern);
            _checkPatterns = null;
            return resultPattern;
        }

        public async Task RemovePattern(string pattern)
        {
            await _scoreSystemRepository.RemovePattern(pattern);
            _checkPatterns = null;
        }

        public async Task<Transaction> Feedback(string clientId, string transactionId, TransactionStatus transactionStatus)
        {
            var transaction = _scoreSystemRepository.GetTransactionById(clientId, transactionId);
            if (transaction != null)
            {
                transaction.TransactionStatus = transactionStatus;
                await _scoreSystemRepository.IncludeOrChangeTransaction(transaction);
                return transaction;
            }
            return null;
        }

        public async Task<double> CheckScore(string clientId, string transactionId, string signature, DateTime transactionDate)
        {
            var currentPattern = new StringBuilder();

            var transaction = new Transaction(clientId, transactionId, signature,transactionDate);
            await _scoreSystemRepository.IncludeOrChangeTransaction(transaction);

            var clientTransactions = _scoreSystemRepository.GetClientTransactions(clientId, transactionDate, MAX_TRANSACTIONS).ToList();
            var maxPatternHistorySize = CheckPatterns.Keys.Max();

            double result = 0;
            for (byte index = 0; index < clientTransactions.Count && index < maxPatternHistorySize; index++)
            {
                var clientTransaction = clientTransactions[index];
                currentPattern.Insert(0, clientTransaction.Signature);
                Dictionary<string, double> patterns;
                if (CheckPatterns.TryGetValue((byte) (index + 1), out patterns))
                {
                    result += patterns
                            .Where(comparePattern => Regex.IsMatch(currentPattern.ToString(), comparePattern.Key))
                            .Sum(comparePattern => comparePattern.Value);
                }
            }

            return result;
        }


    }
}
