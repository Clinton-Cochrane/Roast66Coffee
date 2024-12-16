import React from "react";

function Loading() {
  return (
    <div className="flex justify-center items-center h-64">
      <svg
        width="100"
        height="100"
        viewBox="0 0 100 100"
        xmlns="http://www.w3.org/2000/svg"
      >
        {/* First Circle for Tire Track (Radius 30px) */}
        <circle
          cx="50"
          cy="50"
          r="30"
          fill="none"
          stroke="#555"
          strokeWidth="2"
          className="track1"
        />

        {/* Second Circle for Tire Track (Radius 35px) */}
        <circle
          cx="50"
          cy="50"
          r="35"
          fill="none"
          stroke="#555"
          strokeWidth="2"
          className="track2"
        />

        {/* Truck */}
        <g className="orbit">
        <g className="truck">
          <text x="50" y="50" fontSize="16" textAnchor="middle" dominantBaseline="middle">
            ☕
          </text>
        </g>
        </g>

        <style>
          {`
            /* Common Animation Duration */
            :root {
              --animation-duration: 2s;
              --orbit-radius: 20px
            }

            /* First Tire Track Animation */
            .track1 {
              stroke-dasharray: 188.5;
              stroke-dashoffset: 188.5;
              animation: drawTrack1 var(--animation-duration) linear infinite;
            }

            @keyframes drawTrack1 {
              0% {
                stroke-dashoffset: 188.5;
              }
              100% {
                stroke-dashoffset: 0;
              }
            }

            /* Second Tire Track Animation */
            .track2 {
              stroke-dasharray: 219.9;
              stroke-dashoffset: 219.9;
              animation: drawTrack2 var(--animation-duration) linear infinite;
            }

            @keyframes drawTrack2 {
              0% {
                stroke-dashoffset: 219.9;
              }
              100% {
                stroke-dashoffset: 0;
              }
            }

            /* Truck Animation with Initial Rotation Offset */
            .truck {
              animation: moveTruck var(--animation-duration) linear infinite;
              transform-origin: 50px 50px;
              transform: rotate(90deg); /* Initial offset to align with 3 o'clock */
            }

            @keyframes moveTruck {
              0% {
                transform: rotate(90deg) translate(0, -32px) rotate(-90deg);
              }
              100% {
                transform: rotate(450deg) translate(0, -32px) rotate(-450deg);
              }
            }
          `}
        </style>
      </svg>
    </div>
  );
}

export default Loading;
