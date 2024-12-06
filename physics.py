import numpy as np
from scipy.integrate import solve_ivp

class ThermalSimulation:
    def __init__(self, mass, initial_temp, fridge_temp, htc):
        self.mass = mass
        self.initial_temp = initial_temp
        self.fridge_temp = fridge_temp
        self.htc = htc
        self.specific_heat = 4181  # Specific heat of water (J/kg·K)

    def cooling_rate(self, t, T):
        return -self.htc * (T - self.fridge_temp) / (self.mass * self.specific_heat)

    def run(self):
        # Solve the cooling equation
        result = solve_ivp(
            self.cooling_rate,
            [0, 10000],  # Simulate for a long duration
            [self.initial_temp],
            dense_output=True,
        )

        # Extract time and temperature results
        time = result.t
        temperature = result.y[0]
        return time, temperature
