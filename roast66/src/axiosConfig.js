import axios from 'axios';

const instance = axios.create({
  baseURL: process.env.REACT_APP_API_URL || 'https://roast66coffee.onrender.com/api',
});

export default instance;
