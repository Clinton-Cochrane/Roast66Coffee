import React, { useEffect, useState } from 'react';
import axios from '../../axiosConfig'

function Menu(){
    const [menuItems, setMenuItems] = useState([]);

    useEffect(()=> {
        axios.get('/menu')
            .then(response => setMenuItems(response.data))             
            .catch(error => console.error(error));     
    },[]);
    

    return (
        <div className='menu'>
            <h2>Menu</h2>
            <ul>
                {menuItems.map(item => (
                    <li key={item.id}>{item.name} - ${item.price}</li>
                ))}
            </ul>
        </div>
    );
}

export default Menu;