using System;
using System.Runtime.InteropServices;

namespace ClinicFlow.Api.Native;

/// <summary>
/// P/Invoke bindings for the C simulation engine.
/// Maps to simulation.h structures and functions.
/// </summary>
internal static class SimulationNative
{
    private static string GetLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "simulation.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libsimulation.dylib";
        else
            return "libsimulation.so";
    }

    private static readonly string LibraryName = GetLibraryName();

    static SimulationNative()
    {
        // Load the library from the application directory
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyLocation);
        
        if (!string.IsNullOrEmpty(assemblyDirectory))
        {
            var libraryPath = System.IO.Path.Combine(assemblyDirectory, LibraryName);
            if (System.IO.File.Exists(libraryPath))
            {
                // On macOS, we need to use the full path or set DYLD_LIBRARY_PATH
                // For now, DllImport with just the filename should work if it's in the same directory
            }
        }
    }

    // Structures matching simulation.h

    [StructLayout(LayoutKind.Sequential)]
    public struct Appt
    {
        public float p_no_show;
        public float unit_revenue;
        public ushort service_minutes;
        public byte segment_id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SimConfig
    {
        public ushort providers_per_block;
        public ushort avg_service_minutes;
        public ushort time_block_minutes;
        public uint num_trials;
        public uint random_seed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SimResult
    {
        public float expected_kept;
        public float overload_probability;
        public float expected_wait_minutes;
        public float p95_wait_minutes;
        public float revenue_recovered;
    }

    public enum SimStatus : int
    {
        SIM_OK = 0,
        SIM_ERR_INVALID_CONFIG = 1,
        SIM_ERR_INVALID_PROB = 2,
        SIM_ERR_EMPTY_SCHEDULE = 3,
        SIM_ERR_INTERNAL = 4
    }

    // Function declarations

    [DllImport("libsimulation.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "run_simulation")]
    public static extern SimStatus RunSimulation(
        [In] Appt[] appts,
        uint n_appts,
        [In] ref SimConfig cfg,
        [Out] out SimResult out_result
    );

    [DllImport("libsimulation.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "run_simulation_batch")]
    public static extern SimStatus RunSimulationBatch(
        [In] Appt[] appts,
        uint n_appts,
        [In] ref SimConfig cfg,
        [In] ushort[] strategy_overbook_additions,
        uint n_strategies,
        ushort n_segments,
        [Out] SimResult[] out_results
    );
}

