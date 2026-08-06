using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Deneblab.AbcVersion.Internal;

internal class CliRunner
{
    private readonly ILogger _log;

    public CliRunner(ILogger log)
    {
        _log = log;
    }

    public string RunReturnText(
        string toolPath,
        string arguments,
        string workingDirectory = null,
        int? timeout = null,
        bool logOutput = true,
        int numberLinesToReturn = 1)
    {
        try
        {
            var result = Run(toolPath, arguments, workingDirectory, logOutput: false);
            var text = result.Select(x => x.Text).Take(numberLinesToReturn).Join(Environment.NewLine);
            return text;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Problem with RunReturnText");
            throw;
        }
    }

    public List<Output> Run(
        string toolPath,
        string arguments,
        string workingDirectory = null,
        int? timeout = null,
        bool logOutput = false
    )
    {
        var result = StartProcessInternal(toolPath,
            arguments,
            workingDirectory,
            timeout,
            logOutput);
        if (result == null) throw new NullReferenceException("Problem with process start");
        result.Process.WaitForExit();
        var outPut = result.Output;
        return outPut.ToList();
    }

    private ProcessBox StartProcessInternal(
        string toolPath,
        string arguments,
        string workingDirectory,
        int? timeout,
        bool logOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = arguments,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };


        var process = Process.Start(startInfo);
        if (process == null) return null;
        var output = GetOutputCollection(process, logOutput);
        return new ProcessBox(process, timeout, output);
    }

    private BlockingCollection<Output> GetOutputCollection(
        Process process,
        bool logOutput)
    {
        var output = new BlockingCollection<Output>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;

            output.Add(new Output { Text = e.Data, Type = OutputType.Std });

            if (logOutput) _log.LogTrace($"{e.Data}");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;

            output.Add(new Output { Text = e.Data, Type = OutputType.Err });

            if (logOutput)
                _log.LogTrace($"{e.Data}");
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return output;
    }
}

internal class ProcessBox
{
    public ProcessBox(Process process, int? timeout,
        BlockingCollection<Output> output)
    {
        Process = process;
        Timeout = timeout;
        Output = output;
    }

    public Process Process { get; }
    public int? Timeout { get; }
    public BlockingCollection<Output> Output { get; }
}

internal struct Output
{
    // ReSharper disable once InconsistentNaming

    public OutputType Type;

    // ReSharper disable once InconsistentNaming
    public string Text;
}

internal enum OutputType
{
    Std,
    Err
}