# utils.py - Enhanced with parallel processing and ML utilities

import numpy as np
from scipy.integrate import solve_ivp
from sklearn.ensemble import RandomForestRegressor
from sklearn.cluster import KMeans
from multiprocessing import Pool
import logging

logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")

# Constants
FREEZING_POINT = 0.0  # °C
HEAT_OF_FUSION = 334  # J/g

def cooling_model(t, T, htc, mass, area, fridge_temp):
    """Cooling model based on Newton's Law of Cooling."""
    specific_heat = 4.18  # J/(g°C)
    heat_loss = htc * area * (T - fridge_temp)
    return -heat_loss / (mass * specific_heat)

def simulate_single(params):
    """Simulate cooling for a single set of parameters."""
    mass, initial_temp, fridge_temp, htc, area = params

    def freezing_event(t, T):
        return T - FREEZING_POINT

    freezing_event.terminal = True
    freezing_event.direction = -1

    try:
        result_cooling = solve_ivp(
            cooling_model, 
            t_span=[0, 3600],
            y0=[initial_temp],
            args=(htc, mass, area, fridge_temp),
            events=freezing_event
        )

        freezing_time = result_cooling.t_events[0][0] if result_cooling.t_events[0].size > 0 else np.nan

        if np.isnan(freezing_time):
            return (params, None)

        energy_removal = HEAT_OF_FUSION * mass  # Energy required for freezing
        additional_time = energy_removal / (htc * area * (FREEZING_POINT - fridge_temp))
        total_time = freezing_time + additional_time

        return (params, total_time)
    except Exception as e:
        logging.error(f"Simulation failed for params {params}: {e}")
        return (params, None)

def generate_simulation_data(param_grid, n_processes=4):
    """Generate simulation data in parallel."""
    with Pool(processes=n_processes) as pool:
        results = pool.map(simulate_single, param_grid)
    return results

def train_regression_model(data):
    """Train a regression model on the simulation data."""
    X = np.array([d[0] for d in data if d[1] is not None])
    y = np.array([d[1] for d in data if d[1] is not None])

    model = RandomForestRegressor()
    model.fit(X, y)
    return model

def perform_clustering(data, n_clusters=3):
    """Perform K-Means clustering on the simulation data."""
    X = np.array([d[0] for d in data if d[1] is not None])
    kmeans = KMeans(n_clusters=n_clusters, random_state=42)
    clusters = kmeans.fit_predict(X)
    return clusters, kmeans

